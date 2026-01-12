using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.Services;
using FestiveGuestAPI.Models;
using Azure.Data.Tables;
using System.Security.Claims;

namespace FestiveGuestAPI.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IUserRepository _userRepository;
    private readonly TableServiceClient _tableServiceClient;

    public ChatHub(IUserRepository userRepository, TableServiceClient tableServiceClient)
    {
        _userRepository = userRepository;
        _tableServiceClient = tableServiceClient;
    }

    public async Task JoinChat(string otherUserId)
    {
        var userId = Context.User?.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        var sortedIds = new[] { userId, otherUserId }.OrderBy(x => x).ToArray();
        var chatRoom = $"chat_{sortedIds[0]}_{sortedIds[1]}";
        
        await Groups.AddToGroupAsync(Context.ConnectionId, chatRoom);
    }

    public async Task SendMessage(string otherUserId, string message)
    {
        try
        {
            var userId = Context.User?.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                await Clients.Caller.SendAsync("Error", "Message cannot be empty");
                return;
            }

            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                await Clients.Caller.SendAsync("Error", "User not found");
                return;
            }

            var sortedIds = new[] { userId, otherUserId }.OrderBy(x => x).ToArray();
            var chatRoom = $"chat_{sortedIds[0]}_{sortedIds[1]}";

            var messagesTable = _tableServiceClient.GetTableClient("ChatMessages");
            await messagesTable.CreateIfNotExistsAsync();

            var messageId = Guid.NewGuid().ToString();
            var messageEntity = new ChatMessageEntity
            {
                PartitionKey = chatRoom,
                RowKey = messageId,
                SenderId = userId,
                SenderName = user.Name,
                ReceiverId = otherUserId,
                Message = message,
                Status = "Sent"
            };

            await messagesTable.AddEntityAsync(messageEntity);

            await Clients.Group(chatRoom).SendAsync("ReceiveMessage", new
            {
                id = messageId,
                senderId = userId,
                senderName = user.Name,
                receiverId = otherUserId,
                message = message,
                timestamp = DateTime.UtcNow,
                status = "Sent"
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to send message: {ex.Message}");
        }
    }

    public async Task MarkDelivered(string messageId, string chatRoom)
    {
        try
        {
            var userId = Context.User?.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId)) return;

            var messagesTable = _tableServiceClient.GetTableClient("ChatMessages");
            var entity = await messagesTable.GetEntityAsync<ChatMessageEntity>(chatRoom, messageId);
            
            if (entity.Value.SenderId != userId)
            {
                entity.Value.Status = "Delivered";
                entity.Value.DeliveredAt = DateTime.UtcNow;
                await messagesTable.UpdateEntityAsync(entity.Value, entity.Value.ETag);
                
                await Clients.Group(chatRoom).SendAsync("MessageStatusUpdated", new
                {
                    messageId,
                    status = "Delivered",
                    deliveredAt = entity.Value.DeliveredAt
                });
            }
        }
        catch { }
    }

    public async Task MarkRead(string messageId, string chatRoom)
    {
        try
        {
            var userId = Context.User?.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId)) return;

            var messagesTable = _tableServiceClient.GetTableClient("ChatMessages");
            var entity = await messagesTable.GetEntityAsync<ChatMessageEntity>(chatRoom, messageId);
            
            if (entity.Value.SenderId != userId)
            {
                entity.Value.Status = "Read";
                entity.Value.ReadAt = DateTime.UtcNow;
                await messagesTable.UpdateEntityAsync(entity.Value, entity.Value.ETag);
                
                await Clients.Group(chatRoom).SendAsync("MessageStatusUpdated", new
                {
                    messageId,
                    status = "Read",
                    readAt = entity.Value.ReadAt
                });
            }
        }
        catch { }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}