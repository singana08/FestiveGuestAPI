using Azure;
using Azure.Data.Tables;
using FestiveGuestAPI.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FestiveGuestAPI.Services;

public record StayView(string Id, string Kind, string Status, DateTime StartDate, DateTime EndDate,
    string RequesterId, string RequesterName, string PartnerId, string PartnerName, string HostId, string GuestId, bool Ended);

/// <summary>What the review rules need to know about two members' stays: the latest ended one, or when the next one ends.</summary>
public record StayGate(bool Ended, DateTime? EndsOn, string? StayId = null);

/// <summary>
/// Stay requests: a traveller requests dates (or a host offers them), and only the other person confirms.
/// The request and every answer are posted into their chat, so the conversation carries on around it.
/// A confirmed stay that has ended is what lets them review each other, and counts on their profiles.
/// </summary>
public interface IStayService
{
    Task<(StayView? Stay, string? Error, ChatMessageEntity? Message)> RequestAsync(string requesterId, string partnerId, string? kind, DateTime start, DateTime end, string? note = null);
    Task<(string? Error, ChatMessageEntity? Message)> RespondAsync(string userId, string stayId, bool accept);
    Task<(string? Error, ChatMessageEntity? Message)> CancelAsync(string userId, string stayId);
    Task<List<StayView>> BetweenAsync(string a, string b);
    Task<List<StayView>> MineAsync(string userId);
    Task<StayGate> ReviewGateAsync(string a, string b);
    Task<IReadOnlyDictionary<string, int>> CompletedCountsAsync();
    Task<List<StayEntity>> DueForReminderAsync();
    Task MarkRemindedAsync(StayEntity stay);
}

public class StayService : IStayService
{
    public const string Pending = "Pending", Confirmed = "Confirmed", Declined = "Declined", Cancelled = "Cancelled";
    public const int MaxDays = 60, PastDays = 30, NoteMax = 300;
    private const string CountsKey = "stay-counts";

    private readonly TableServiceClient _tables;
    private readonly IUserRepository _users;
    private readonly ISafetyRepository _safety;
    private readonly IEmailService _email;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StayService> _logger;

    public StayService(TableServiceClient tables, IUserRepository users, ISafetyRepository safety, IEmailService email, IMemoryCache cache, ILogger<StayService> logger)
    {
        _tables = tables;
        _users = users;
        _safety = safety;
        _email = email;
        _cache = cache;
        _logger = logger;
    }

    private TableClient Stays => _tables.GetTableClient("Stays");
    private static DateTime Today => DateTime.UtcNow.Date;
    private static DateTime DateOnlyUtc(DateTime d) => DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
    private static string FirstName(string name) => (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "them";

    /// <summary>"20 Dec – 23 Dec 2026", or "20 Dec 2026" for a single day.</summary>
    public static string Dates(DateTime start, DateTime end)
    {
        var c = System.Globalization.CultureInfo.InvariantCulture;
        return start.Date == end.Date ? start.ToString("d MMM yyyy", c) : $"{start.ToString("d MMM", c)} – {end.ToString("d MMM yyyy", c)}";
    }

    private static string Word(StayEntity s) => s.Kind == "meetup" ? "meet-up" : "stay";

    /// <summary>The chat message a request becomes, e.g. "📅 Stay request: 27 Nov – 28 Nov 2026 (1 night)" plus the note.</summary>
    public static string RequestText(StayEntity s, string? note)
    {
        var fromHost = s.RequesterId == s.HostId;
        var what = s.Kind == "meetup" ? (fromHost ? "Offer to meet" : "Meet-up request") : (fromHost ? "Offer to host you" : "Stay request");
        var nights = (s.EndDate.Date - s.StartDate.Date).Days;
        var length = s.Kind == "stay" && nights > 0 ? $" ({nights} night{(nights == 1 ? "" : "s")})" : "";
        var text = $"📅 {what}: {Dates(s.StartDate, s.EndDate)}{length}";
        return string.IsNullOrWhiteSpace(note) ? text : $"{text}\n{note.Trim()}";
    }

    public static StayView View(StayEntity s) =>
        new(s.RowKey, s.Kind, s.Status, s.StartDate, s.EndDate, s.RequesterId, s.RequesterName, s.PartnerId, s.PartnerName, s.HostId, s.GuestId,
            s.Status == Confirmed && s.EndDate.Date <= Today);

    private static async Task<List<StayEntity>> AllAsync(AsyncPageable<StayEntity> query)
    {
        var list = new List<StayEntity>();
        try { await foreach (var s in query) list.Add(s); }
        catch (RequestFailedException ex) when (ex.Status == 404) { }
        return list;
    }

    private Task<List<StayEntity>> PairAsync(string a, string b) => AllAsync(Stays.QueryAsync<StayEntity>(s =>
        s.PartitionKey == "STAY" && ((s.RequesterId == a && s.PartnerId == b) || (s.RequesterId == b && s.PartnerId == a))));

    private async Task<StayEntity?> GetAsync(string id)
    {
        try { return (await Stays.GetEntityAsync<StayEntity>("STAY", id)).Value; }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }

    private async Task SaveAsync(StayEntity stay)
    {
        await Stays.UpsertEntityAsync(stay, TableUpdateMode.Replace);
        _cache.Remove(CountsKey);
    }

    public async Task<(StayView? Stay, string? Error, ChatMessageEntity? Message)> RequestAsync(
        string requesterId, string partnerId, string? kind, DateTime start, DateTime end, string? note = null)
    {
        if (requesterId == partnerId) return (null, "You can't request a stay with yourself.", null);
        var requester = await _users.GetUserByIdAsync(requesterId);
        var partner = await _users.GetUserByIdAsync(partnerId);
        if (requester == null || partner == null || partner.Status != "Active") return (null, "This member isn't available.", null);
        var types = new[] { requester.UserType, partner.UserType };
        if (!types.Contains("Host") || !types.Contains("Guest")) return (null, "Stays are between a host and a traveller.", null);
        if (await _safety.IsBlockedEitherWayAsync(requesterId, partnerId)) return (null, "You can't request a stay with this member.", null);
        // No "chat first": like Request to book, the request is what starts the conversation.

        start = DateOnlyUtc(start);
        end = DateOnlyUtc(end);
        if (end < start) return (null, "The end date is before the start date.", null);
        if (start < Today.AddDays(-PastDays)) return (null, $"Pick dates from the last {PastDays} days onwards.", null);
        if (start > Today.AddDays(365)) return (null, "Pick dates within the next year.", null);
        if ((end - start).TotalDays > MaxDays) return (null, $"A stay can be up to {MaxDays} days.", null);
        if ((note ?? "").Trim().Length > NoteMax) return (null, $"Please keep the note under {NoteMax} characters.", null);
        if ((await PairAsync(requesterId, partnerId)).Any(s => s.Status == Pending))
            return (null, "There's already a request waiting for an answer.", null);

        var host = requester.UserType == "Host" ? requester : partner;
        var stay = new StayEntity
        {
            RowKey = Guid.NewGuid().ToString("N"),
            RequesterId = requesterId, RequesterName = requester.Name,
            PartnerId = partnerId, PartnerName = partner.Name,
            HostId = host.RowKey, GuestId = host == requester ? partnerId : requesterId,
            Kind = kind == "meetup" ? "meetup" : "stay",
            StartDate = start, EndDate = end,
            Status = Pending, CreatedAt = DateTime.UtcNow,
        };
        await Stays.CreateIfNotExistsAsync();
        await SaveAsync(stay);
        var message = await ChatHistory.PostAsync(_tables, requesterId, requester.Name, partnerId, RequestText(stay, note));

        try { await _email.SendStayRequestAsync(partner.Email, partner.Name, requester.Name, stay.Kind, Dates(start, end)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Couldn't email stay request {StayId}", stay.RowKey); }
        return (View(stay), null, message);
    }

    /// <summary>Posts "what just happened" into their chat, from the person who did it.</summary>
    private Task<ChatMessageEntity> TellAsync(StayEntity stay, string byId, string text)
    {
        var (byName, toId) = byId == stay.RequesterId ? (stay.RequesterName, stay.PartnerId) : (stay.PartnerName, stay.RequesterId);
        return ChatHistory.PostAsync(_tables, byId, byName, toId, $"{text}: {Dates(stay.StartDate, stay.EndDate)}");
    }

    public async Task<(string? Error, ChatMessageEntity? Message)> RespondAsync(string userId, string stayId, bool accept)
    {
        var stay = await GetAsync(stayId);
        if (stay == null || (stay.PartnerId != userId && stay.RequesterId != userId)) return ("Request not found.", null);
        if (stay.PartnerId != userId) return ($"Waiting for {FirstName(stay.PartnerName)} to answer.", null);
        if (stay.Status != Pending) return ("This request was already answered.", null);

        stay.Status = accept ? Confirmed : Declined;
        stay.RespondedAt = DateTime.UtcNow;
        await SaveAsync(stay);
        var message = await TellAsync(stay, userId, accept ? $"✅ Confirmed the {Word(stay)}" : $"Declined the {Word(stay)} request");

        if (accept)
        {
            var requester = await _users.GetUserByIdAsync(stay.RequesterId);
            try { if (requester != null) await _email.SendStayConfirmedAsync(requester.Email, requester.Name, stay.PartnerName, stay.Kind, Dates(stay.StartDate, stay.EndDate)); }
            catch (Exception ex) { _logger.LogWarning(ex, "Couldn't email stay confirmation {StayId}", stay.RowKey); }
        }
        return (null, message);
    }

    public async Task<(string? Error, ChatMessageEntity? Message)> CancelAsync(string userId, string stayId)
    {
        var stay = await GetAsync(stayId);
        if (stay == null || (stay.PartnerId != userId && stay.RequesterId != userId)) return ("Request not found.", null);
        // Once a confirmed stay has started it stays on record, so nobody can cancel their way out of a review.
        if (stay.Status == Confirmed && stay.StartDate.Date <= Today) return ("This stay has already started, so it can't be cancelled.", null);
        if (stay.Status is not (Pending or Confirmed)) return ("This can't be cancelled.", null);

        var wasConfirmed = stay.Status == Confirmed;
        stay.Status = Cancelled;
        stay.RespondedAt = DateTime.UtcNow;
        await SaveAsync(stay);
        return (null, await TellAsync(stay, userId, wasConfirmed ? $"Cancelled the {Word(stay)}" : $"Cancelled the {Word(stay)} request"));
    }

    public async Task<List<StayView>> BetweenAsync(string a, string b) =>
        (await PairAsync(a, b)).OrderByDescending(s => s.CreatedAt).Select(View).ToList();

    public async Task<List<StayView>> MineAsync(string userId) =>
        (await AllAsync(Stays.QueryAsync<StayEntity>(s => s.PartitionKey == "STAY" && (s.RequesterId == userId || s.PartnerId == userId))))
            .OrderByDescending(s => s.StartDate).Select(View).ToList();

    public async Task<StayGate> ReviewGateAsync(string a, string b)
    {
        var confirmed = (await PairAsync(a, b)).Where(s => s.Status == Confirmed).ToList();
        var ended = confirmed.Where(s => s.EndDate.Date <= Today).OrderByDescending(s => s.EndDate).FirstOrDefault();
        if (ended != null) return new(true, null, ended.RowKey);
        return new(false, confirmed.Count > 0 ? confirmed.Min(s => s.EndDate) : null);
    }

    public async Task<IReadOnlyDictionary<string, int>> CompletedCountsAsync() =>
        (await _cache.GetOrCreateAsync(CountsKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var done = (await AllAsync(Stays.QueryAsync<StayEntity>(s => s.PartitionKey == "STAY" && s.Status == Confirmed)))
                .Where(s => s.EndDate.Date <= Today);
            return (IReadOnlyDictionary<string, int>)done.SelectMany(s => new[] { s.HostId, s.GuestId })
                .GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        }))!;

    public async Task<List<StayEntity>> DueForReminderAsync() =>
        // Table Storage can't filter on a missing (null) column, so the reminder check runs here.
        (await AllAsync(Stays.QueryAsync<StayEntity>(s => s.PartitionKey == "STAY" && s.Status == Confirmed)))
            .Where(s => s.EndDate.Date <= Today && s.ReminderSentAt == null).ToList();

    public async Task MarkRemindedAsync(StayEntity stay)
    {
        stay.ReminderSentAt = DateTime.UtcNow;
        await SaveAsync(stay);
    }
}
