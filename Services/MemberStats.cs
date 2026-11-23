using Azure;
using Azure.Data.Tables;
using FestiveGuestAPI.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FestiveGuestAPI.Services;

/// <summary>"Usually replies within a few hours" — worked out from chat history.</summary>
public interface IMemberStats
{
    Task<IReadOnlyDictionary<string, string>> ResponseBadgesAsync();
}

public class MemberStats : IMemberStats
{
    private readonly TableServiceClient _tables;
    private readonly IMemoryCache _cache;

    public MemberStats(TableServiceClient tables, IMemoryCache cache)
    {
        _tables = tables;
        _cache = cache;
    }

    public async Task<IReadOnlyDictionary<string, string>> ResponseBadgesAsync() =>
        (await _cache.GetOrCreateAsync("response-badges", async entry =>
        {
            // ponytail: scans every chat message once an hour per server; keep per-member counters if chats grow large.
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var messages = new List<ChatMessageEntity>();
            try
            {
                await foreach (var m in _tables.GetTableClient("ChatMessages")
                    .QueryAsync<ChatMessageEntity>(select: new[] { "PartitionKey", "RowKey", "SenderId", "ReceiverId", "SentAt", "Timestamp" }))
                    messages.Add(m);
            }
            catch (RequestFailedException ex) when (ex.Status == 404) { }
            return Compute(messages, DateTime.UtcNow);
        }))!;

    /// <summary>
    /// For each conversation in the last 180 days, the person who received the first message is the responder;
    /// their response time is until their first message in it. A badge needs at least 2 such conversations,
    /// replies to at least half of them, and a median reply time of a day or less.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Compute(IEnumerable<ChatMessageEntity> messages, DateTime now)
    {
        var since = now.AddDays(-180);
        var byResponder = new Dictionary<string, List<TimeSpan?>>(); // null = never replied

        foreach (var conversation in messages.GroupBy(m => m.PartitionKey))
        {
            // ponytail: older messages have no SentAt; their Timestamp moves when they're read, so those times are approximate.
            var timeline = conversation
                .Select(m => (Message: m, At: m.SentAt ?? m.Timestamp?.UtcDateTime))
                .Where(x => x.At != null)
                .OrderBy(x => x.At)
                .ToList();
            if (timeline.Count == 0 || timeline[0].At < since) continue;

            var first = timeline[0];
            var responder = first.Message.ReceiverId;
            if (string.IsNullOrEmpty(responder) || responder == first.Message.SenderId) continue;

            var reply = timeline.FirstOrDefault(x => x.Message.SenderId == responder);
            if (!byResponder.TryGetValue(responder, out var times)) byResponder[responder] = times = new();
            times.Add(reply.Message == null ? null : reply.At - first.At);
        }

        var badges = new Dictionary<string, string>();
        foreach (var (userId, times) in byResponder)
        {
            var replied = times.Where(t => t != null).Select(t => t!.Value).OrderBy(t => t).ToList();
            if (times.Count < 2 || replied.Count * 2 < times.Count) continue;
            var median = replied[replied.Count / 2];
            var badge = median <= TimeSpan.FromHours(1) ? "Usually replies within an hour"
                : median <= TimeSpan.FromHours(6) ? "Usually replies within a few hours"
                : median <= TimeSpan.FromHours(24) ? "Usually replies within a day"
                : null;
            if (badge != null) badges[userId] = badge;
        }
        return badges;
    }
}
