using Azure;
using Azure.Data.Tables;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

/// <summary>Questions about two members' chat that other features ask.</summary>
public static class ChatHistory
{
    /// <summary>Same room name the chat uses (ChatController/ChatHub).</summary>
    public static string Room(string a, string b)
    {
        var ids = new[] { a, b }.OrderBy(x => x).ToArray();
        return $"chat_{ids[0]}_{ids[1]}";
    }

    private static async Task<bool> HasSentAsync(TableClient messages, string room, string senderId)
    {
        try
        {
            await foreach (var _ in messages.QueryAsync<ChatMessageEntity>(m => m.PartitionKey == room && m.SenderId == senderId, maxPerPage: 1, select: new[] { "SenderId" }))
                return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { }
        return false;
    }

    /// <summary>Adds a message to two members' chat (e.g. a stay request), exactly as if it had been sent from the chat.</summary>
    public static async Task<ChatMessageEntity> PostAsync(TableServiceClient tables, string fromId, string fromName, string toId, string text)
    {
        var messages = tables.GetTableClient("ChatMessages");
        await messages.CreateIfNotExistsAsync();
        var message = new ChatMessageEntity
        {
            PartitionKey = Room(fromId, toId), RowKey = Guid.NewGuid().ToString(),
            SenderId = fromId, SenderName = fromName, ReceiverId = toId,
            Message = text, Status = "Sent", SentAt = DateTime.UtcNow,
        };
        await messages.AddEntityAsync(message);
        return message;
    }

    /// <summary>The shape the chat sends over SignalR ("ReceiveMessage").</summary>
    public static object Payload(ChatMessageEntity m) => new
    {
        id = m.RowKey, senderId = m.SenderId, senderName = m.SenderName, receiverId = m.ReceiverId,
        message = m.Message, timestamp = m.SentAt ?? DateTime.UtcNow, status = m.Status, about = About(m),
    };

    /// <summary>"About: (post title)" on a message sent from a post's card; null otherwise.</summary>
    public static object? About(ChatMessageEntity m) =>
        string.IsNullOrEmpty(m.AboutPostId) ? null : new { postId = m.AboutPostId, kind = m.AboutKind, title = m.AboutTitle };

    /// <summary>
    /// Marks a message as sent about a post, before it's saved. Only a live post of one of the two members counts.
    /// A message to a trip's traveller also adds the sender to that trip's "messaged you about this" (PostActivity).
    /// Never throws: a message is never held up by this.
    /// </summary>
    public static async Task AboutAsync(ChatMessageEntity m, string? postId, IGuestPostRepository trips, IHostPostRepository listings, TableServiceClient tables)
    {
        if (string.IsNullOrWhiteSpace(postId)) return;
        try
        {
            var members = new[] { m.SenderId, m.ReceiverId };
            if (await trips.GetByIdAsync(postId) is { Status: "Active" } trip && members.Contains(trip.UserId))
            {
                (m.AboutPostId, m.AboutKind, m.AboutTitle) = (trip.RowKey, "trip", trip.Title);
                if (trip.UserId == m.ReceiverId) await PostActivity.AddContactAsync(tables, trip.RowKey, m.SenderId);
            }
            else if (await listings.GetByIdAsync(postId) is { Status: "Active" } listing && members.Contains(listing.UserId))
                (m.AboutPostId, m.AboutKind, m.AboutTitle) = (listing.RowKey, "listing", listing.Title);
        }
        catch { /* the message still goes, just without "About" */ }
    }

    public static async Task<bool> HaveBothMessagedAsync(TableServiceClient tables, string a, string b)
    {
        var messages = tables.GetTableClient("ChatMessages");
        var room = Room(a, b);
        return await HasSentAsync(messages, room, a) && await HasSentAsync(messages, room, b);
    }
}
