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
        message = m.Message, timestamp = m.SentAt ?? DateTime.UtcNow, status = m.Status,
    };

    public static async Task<bool> HaveBothMessagedAsync(TableServiceClient tables, string a, string b)
    {
        var messages = tables.GetTableClient("ChatMessages");
        var room = Room(a, b);
        return await HasSentAsync(messages, room, a) && await HasSentAsync(messages, room, b);
    }
}
