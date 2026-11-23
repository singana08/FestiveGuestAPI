using Azure;
using Azure.Data.Tables;
using FestiveGuestAPI.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FestiveGuestAPI.Services;

/// <summary>Legacy = written before "Confirm stay" existed (no stay behind it), so never published.</summary>
public record ReviewView(string Id, string RevieweeId, string ReviewerId, string ReviewerName, string ReviewerRole,
    int Rating, string Comment, DateTime CreatedAt, string Reply, DateTime? RepliedAt, bool Visible, bool Legacy = false);

/// <summary>What the profile shows next to "Write a review".</summary>
public record ReviewStatus(bool CanReview, string? Reason, ReviewView? Mine, bool TheyReviewedYou);

/// <summary>
/// Two-way reviews (guest ↔ host) that can be trusted:
/// only after a stay or meet-up both confirmed has ended (see StayService), one per pair, and hidden until
/// the other person has reviewed back or 14 days have passed — so nobody writes theirs after reading the other one.
/// A hidden review can be edited; a published one can't. The person reviewed can reply once.
/// </summary>
public interface IReviewService
{
    Task<ReviewStatus> StatusAsync(string reviewerId, string revieweeId);
    Task<string?> SubmitAsync(string reviewerId, string revieweeId, int rating, string? comment);
    Task<List<ReviewView>> VisibleAboutAsync(string revieweeId);
    Task<string?> ReplyAsync(string revieweeId, string reviewId, string? text);
    Task<IReadOnlyDictionary<string, (double Average, int Count)>> SummariesAsync();
    Task<(List<ReviewView> About, List<ReviewView> By)> AllForAdminAsync(string userId);
    Task<bool> DeleteAsync(string revieweeId, string reviewId);
    Task<HashSet<string>> ReviewedByAsync(string reviewerId);
}

public class ReviewService : IReviewService
{
    public static readonly TimeSpan RevealAfter = TimeSpan.FromDays(14);
    public const int CommentMin = 10, CommentMax = 1000, ReplyMax = 500;
    private const string SummariesKey = "review-summaries";

    private readonly TableServiceClient _tables;
    private readonly IUserRepository _users;
    private readonly ISafetyRepository _safety;
    private readonly IStayService _stays;
    private readonly IMemoryCache _cache;

    public ReviewService(TableServiceClient tables, IUserRepository users, ISafetyRepository safety, IStayService stays, IMemoryCache cache)
    {
        _tables = tables;
        _users = users;
        _safety = safety;
        _stays = stays;
        _cache = cache;
    }

    private TableClient Reviews => _tables.GetTableClient("Reviews");

    /// <summary>Only connected people review: a review counts only if it belongs to a confirmed stay.</summary>
    public static bool FromAStay(ReviewEntity review) => !string.IsNullOrEmpty(review.StayId);

    public static bool IsVisible(ReviewEntity review, bool reviewedBack, DateTime now) =>
        FromAStay(review) && (reviewedBack || now - review.CreatedAt >= RevealAfter);

    private static ReviewView View(ReviewEntity r, bool visible) =>
        new(r.RowKey, r.PartitionKey, r.ReviewerId, r.ReviewerName, r.ReviewerRole, r.Rating, r.Comment, r.CreatedAt, r.Reply, r.RepliedAt, visible, !FromAStay(r));

    private static string FirstName(string name) => (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "them";

    /// <summary>Reads that treat a table that doesn't exist yet as empty.</summary>
    private static async Task<List<T>> AllAsync<T>(AsyncPageable<T> query)
    {
        var list = new List<T>();
        try { await foreach (var item in query) list.Add(item); }
        catch (RequestFailedException ex) when (ex.Status == 404) { }
        return list;
    }

    private Task<List<ReviewEntity>> AboutAsync(string userId) => AllAsync(Reviews.QueryAsync<ReviewEntity>(r => r.PartitionKey == userId));
    private Task<List<ReviewEntity>> ByAsync(string userId) => AllAsync(Reviews.QueryAsync<ReviewEntity>(r => r.ReviewerId == userId));

    /// <summary>Members this person has reviewed (for "Reviewed ✓" on their trips).</summary>
    public async Task<HashSet<string>> ReviewedByAsync(string reviewerId) =>
        (await ByAsync(reviewerId)).Where(FromAStay).Select(r => r.PartitionKey).ToHashSet();

    private async Task<ReviewEntity?> FindAsync(string revieweeId, string reviewerId) =>
        (await AllAsync(Reviews.QueryAsync<ReviewEntity>(r => r.PartitionKey == revieweeId && r.ReviewerId == reviewerId))).FirstOrDefault();

    public async Task<ReviewStatus> StatusAsync(string reviewerId, string revieweeId)
    {
        if (reviewerId == revieweeId) return new(false, "You can't review yourself.", null, false);
        var reviewee = await _users.GetUserByIdAsync(revieweeId);
        if (reviewee == null || reviewee.Status != "Active") return new(false, "This member can't be reviewed right now.", null, false);

        var now = DateTime.UtcNow;
        var mine = await FindAsync(revieweeId, reviewerId);
        var theirs = await FindAsync(reviewerId, revieweeId);
        var mineCounts = mine != null && FromAStay(mine);
        var theirsCounts = theirs != null && FromAStay(theirs);
        var mineView = mine == null ? null : View(mine, IsVisible(mine, theirsCounts, now));
        var theyReviewedYou = theirsCounts && !IsVisible(theirs!, mineCounts, now);

        if (mineView?.Visible == true) return new(false, "Your review is published, so it can't be changed.", mineView, false);
        if (await _safety.IsBlockedEitherWayAsync(reviewerId, revieweeId)) return new(false, "You can't review this member.", mineView, false);
        var gate = await _stays.ReviewGateAsync(reviewerId, revieweeId);
        if (!gate.Ended)
            return new(false, gate.EndsOn is { } endsOn
                ? $"You can review {FirstName(reviewee.Name)} after your stay ends on {StayService.Dates(endsOn, endsOn)}."
                : $"You can review {FirstName(reviewee.Name)} after a stay or meet-up you've both confirmed. Request one from your chat.",
                mineView, theyReviewedYou);
        return new(true, null, mineView, theyReviewedYou);
    }

    public async Task<string?> SubmitAsync(string reviewerId, string revieweeId, int rating, string? comment)
    {
        if (rating is < 1 or > 5) return "Choose between 1 and 5 stars.";
        var text = (comment ?? "").Trim();
        if (text.Length < CommentMin) return "Please write a few words about your experience.";
        if (text.Length > CommentMax) return $"Please keep your review under {CommentMax} characters.";

        var status = await StatusAsync(reviewerId, revieweeId);
        if (!status.CanReview) return status.Reason;

        var reviewer = await _users.GetUserByIdAsync(reviewerId);
        var review = await FindAsync(revieweeId, reviewerId)
            ?? new ReviewEntity { PartitionKey = revieweeId, RowKey = Guid.NewGuid().ToString(), ReviewerId = reviewerId, CreatedAt = DateTime.UtcNow };
        if (!FromAStay(review)) review.CreatedAt = DateTime.UtcNow; // an old pre-stay review starts its 14 days now
        review.StayId = (await _stays.ReviewGateAsync(reviewerId, revieweeId)).StayId ?? "";
        // Editing keeps CreatedAt, so the 14-day clock can't be restarted to keep a review hidden.
        review.ReviewerName = reviewer?.Name ?? "Member";
        review.ReviewerRole = reviewer?.UserType ?? "";
        review.Rating = rating;
        review.Comment = text;

        await Reviews.CreateIfNotExistsAsync();
        await Reviews.UpsertEntityAsync(review, TableUpdateMode.Replace);
        _cache.Remove(SummariesKey);
        return null;
    }

    public async Task<List<ReviewView>> VisibleAboutAsync(string revieweeId)
    {
        var about = await AboutAsync(revieweeId);
        if (about.Count == 0) return new();
        var reviewedBack = (await ByAsync(revieweeId)).Where(FromAStay).Select(r => r.PartitionKey).ToHashSet();
        var now = DateTime.UtcNow;
        return about.Where(r => IsVisible(r, reviewedBack.Contains(r.ReviewerId), now))
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => View(r, true))
            .ToList();
    }

    public async Task<string?> ReplyAsync(string revieweeId, string reviewId, string? text)
    {
        var reply = (text ?? "").Trim();
        if (reply.Length == 0) return "Write a reply first.";
        if (reply.Length > ReplyMax) return $"Please keep your reply under {ReplyMax} characters.";

        ReviewEntity review;
        try { review = (await Reviews.GetEntityAsync<ReviewEntity>(revieweeId, reviewId)).Value; }
        catch (RequestFailedException ex) when (ex.Status == 404) { return "Review not found."; }

        if (!string.IsNullOrEmpty(review.Reply)) return "You've already replied to this review.";
        var back = await FindAsync(review.ReviewerId, revieweeId);
        if (!IsVisible(review, back != null && FromAStay(back), DateTime.UtcNow))
            return "You can reply once the review is published.";

        review.Reply = reply;
        review.RepliedAt = DateTime.UtcNow;
        await Reviews.UpdateEntityAsync(review, review.ETag, TableUpdateMode.Replace);
        return null;
    }

    public async Task<IReadOnlyDictionary<string, (double Average, int Count)>> SummariesAsync() =>
        (await _cache.GetOrCreateAsync(SummariesKey, async entry =>
        {
            // ponytail: reads every review, cached 10 minutes; keep a running total per member if reviews reach the tens of thousands.
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var all = await AllAsync(Reviews.QueryAsync<ReviewEntity>());
            var pairs = all.Where(FromAStay).Select(r => (Reviewer: r.ReviewerId, Reviewee: r.PartitionKey)).ToHashSet();
            var now = DateTime.UtcNow;
            return (IReadOnlyDictionary<string, (double, int)>)all
                .Where(r => IsVisible(r, pairs.Contains((r.PartitionKey, r.ReviewerId)), now))
                .GroupBy(r => r.PartitionKey)
                .ToDictionary(g => g.Key, g => (Math.Round(g.Average(r => r.Rating), 1), g.Count()));
        }))!;

    public async Task<(List<ReviewView> About, List<ReviewView> By)> AllForAdminAsync(string userId)
    {
        var about = await AboutAsync(userId);
        var by = await ByAsync(userId);
        var now = DateTime.UtcNow;
        var theyReviewedBack = by.Where(FromAStay).Select(r => r.PartitionKey).ToHashSet();
        var reviewersOfUser = about.Where(FromAStay).Select(r => r.ReviewerId).ToHashSet();
        return (
            about.OrderByDescending(r => r.CreatedAt).Select(r => View(r, IsVisible(r, theyReviewedBack.Contains(r.ReviewerId), now))).ToList(),
            by.OrderByDescending(r => r.CreatedAt).Select(r => View(r, IsVisible(r, reviewersOfUser.Contains(r.PartitionKey), now))).ToList());
    }

    public async Task<bool> DeleteAsync(string revieweeId, string reviewId)
    {
        // The SDK reports a missing entity as a 404 response rather than throwing.
        try { if ((await Reviews.DeleteEntityAsync(revieweeId, reviewId, ETag.All)).Status == 404) return false; }
        catch (RequestFailedException ex) when (ex.Status == 404) { return false; }
        _cache.Remove(SummariesKey);
        return true;
    }
}
