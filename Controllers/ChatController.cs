using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.Services;
using FestiveGuestAPI.Models;
using Azure.Data.Tables;
using System.Security.Claims;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly TableServiceClient _tableServiceClient;
    private readonly ISafetyRepository _safety;
    private readonly INotificationService _notifications;

    public ChatController(IUserRepository userRepository, TableServiceClient tableServiceClient, ISafetyRepository safety, INotificationService notifications)
    {
        _userRepository = userRepository;
        _tableServiceClient = tableServiceClient;
        _safety = safety;
        _notifications = notifications;
    }

    private const string BlockedMessage = "You can't message this member.";

    [HttpPost("start-chat")]
    public async Task<IActionResult> StartChat([FromBody] StartChatRequest request)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var currentUser = await _userRepository.GetUserByIdAsync(userId);
            var otherUser = await _userRepository.GetUserByIdAsync(request.OtherUserId);

            if (currentUser == null || otherUser == null)
            {
                return BadRequest("Users not found");
            }

            if (await _safety.IsBlockedEitherWayAsync(userId, request.OtherUserId))
            {
                return StatusCode(403, new { message = BlockedMessage });
            }

            var sortedIds = new[] { userId, request.OtherUserId }.OrderBy(x => x).ToArray();
            var chatRoom = $"chat_{sortedIds[0]}_{sortedIds[1]}";

            return Ok(new { 
                chatRoom,
                participants = new[] {
                    new { id = userId, name = currentUser.Name },
                    new { id = request.OtherUserId, name = otherUser.Name }
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to start chat: {ex.Message}");
        }
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request,
        [FromServices] IGuestPostRepository trips, [FromServices] IHostPostRepository listings)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                return NotFound("User not found");

            var recipientId = request.GetRecipientId();
            if (string.IsNullOrEmpty(recipientId))
                return BadRequest("RecipientId is required");

            if (await _safety.IsBlockedEitherWayAsync(userId, recipientId))
                return StatusCode(403, new { success = false, message = BlockedMessage });

            var sortedIds = new[] { userId, recipientId }.OrderBy(x => x).ToArray();
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
                ReceiverId = recipientId,
                Message = request.Message,
                Status = "Sent",
                SentAt = DateTime.UtcNow
            };
            await ChatHistory.AboutAsync(messageEntity, request.AboutPostId, trips, listings, _tableServiceClient);

            await messagesTable.AddEntityAsync(messageEntity);
            _ = Task.Run(() => _notifications.NotifyNewMessageAsync(userId, user.Name, recipientId));

            return Ok(new
            {
                success = true,
                id = messageId,
                senderId = userId,
                senderName = user.Name,
                receiverId = recipientId,
                message = request.Message,
                timestamp = DateTime.UtcNow,
                status = "Sent",
                about = ChatHistory.About(messageEntity)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Failed to send message: {ex.Message}" });
        }
    }

    [HttpGet("messages/{otherUserId}")]
    public async Task<IActionResult> GetMessages(string otherUserId)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var messagesTable = _tableServiceClient.GetTableClient("ChatMessages");
            await messagesTable.CreateIfNotExistsAsync();

            var sortedIds = new[] { userId, otherUserId }.OrderBy(x => x).ToArray();
            var chatRoom = $"chat_{sortedIds[0]}_{sortedIds[1]}";

            var messages = new List<object>();
            await foreach (var entity in messagesTable.QueryAsync<ChatMessageEntity>(
                filter: $"PartitionKey eq '{chatRoom}'"))
            {
                messages.Add(new {
                    id = entity.RowKey,
                    senderId = entity.SenderId,
                    senderName = entity.SenderName,
                    receiverId = entity.ReceiverId,
                    message = entity.Message,
                    timestamp = entity.Timestamp,
                    status = entity.Status,
                    deliveredAt = entity.DeliveredAt,
                    readAt = entity.ReadAt,
                    about = ChatHistory.About(entity)
                });
            }

            return Ok(messages.OrderBy(m => ((DateTimeOffset)((dynamic)m).timestamp)).ToList());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to get messages: {ex.Message}");
        }
    }

    [HttpPost("mark-delivered/{messageId}")]
    public async Task<IActionResult> MarkDelivered(string messageId, [FromBody] MarkStatusRequest request)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var messagesTable = _tableServiceClient.GetTableClient("ChatMessages");
            var entity = await messagesTable.GetEntityAsync<ChatMessageEntity>(request.ChatRoom, messageId);
            
            if (entity.Value.SenderId != userId)
            {
                entity.Value.Status = "Delivered";
                entity.Value.DeliveredAt = DateTime.UtcNow;
                await messagesTable.UpdateEntityAsync(entity.Value, entity.Value.ETag);
                return Ok(new { status = "Delivered", deliveredAt = entity.Value.DeliveredAt });
            }

            return Ok(new { message = "Cannot mark own message as delivered" });
        }
        catch (Exception ex) 
        { 
            return StatusCode(500, new { error = ex.Message }); 
        }
    }

    [HttpPost("mark-read/{messageId}")]
    public async Task<IActionResult> MarkRead(string messageId, [FromBody] MarkStatusRequest request)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var messagesTable = _tableServiceClient.GetTableClient("ChatMessages");
            var entity = await messagesTable.GetEntityAsync<ChatMessageEntity>(request.ChatRoom, messageId);
            
            if (entity.Value.SenderId != userId)
            {
                entity.Value.Status = "Read";
                entity.Value.ReadAt = DateTime.UtcNow;
                await messagesTable.UpdateEntityAsync(entity.Value, entity.Value.ETag);
                return Ok(new { status = "Read", readAt = entity.Value.ReadAt });
            }

            return Ok(new { message = "Cannot mark own message as read" });
        }
        catch (Exception ex) 
        { 
            return StatusCode(500, new { error = ex.Message }); 
        }
    }

    [HttpPost("mark-read-all/{otherUserId}")]
    public async Task<IActionResult> MarkAllRead(string otherUserId)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var messagesTable = _tableServiceClient.GetTableClient("ChatMessages");
            await messagesTable.CreateIfNotExistsAsync();

            var sortedIds = new[] { userId, otherUserId }.OrderBy(x => x).ToArray();
            var chatRoom = $"chat_{sortedIds[0]}_{sortedIds[1]}";

            var markedCount = 0;
            await foreach (var msg in messagesTable.QueryAsync<ChatMessageEntity>(
                filter: $"PartitionKey eq '{chatRoom}' and SenderId eq '{otherUserId}' and Status ne 'Read'"))
            {
                msg.Status = "Read";
                msg.ReadAt = DateTime.UtcNow;
                await messagesTable.UpdateEntityAsync(msg, msg.ETag);
                markedCount++;
            }

            return Ok(new { success = true, markedCount });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var messagesTable = _tableServiceClient.GetTableClient("ChatMessages");
            await messagesTable.CreateIfNotExistsAsync();

            var userMessages = new List<ChatMessageEntity>();
            await foreach (var msg in messagesTable.QueryAsync<ChatMessageEntity>())
            {
                if (!msg.PartitionKey.StartsWith("chat_")) continue;
                var ids = msg.PartitionKey.Replace("chat_", "").Split('_');
                if (ids.Length == 2 && ids.Contains(userId))
                    userMessages.Add(msg);
            }

            var hidden = await _safety.GetHiddenUserIdsAsync(userId);
            var conversations = userMessages
                .GroupBy(m => m.PartitionKey.Replace("chat_", "").Split('_').First(id => id != userId))
                .Where(g => !hidden.Contains(g.Key))
                .Select(g => {
                    var latest = g.OrderByDescending(m => m.Timestamp).First();
                    return new {
                        chatRoom = latest.PartitionKey,
                        otherUserId = g.Key,
                        lastMessage = latest.Message,
                        lastSenderId = latest.SenderId,
                        timestamp = latest.Timestamp ?? DateTimeOffset.MinValue,
                        unreadCount = g.Count(m => m.SenderId == g.Key && m.Status != "Read")
                    };
                })
                .OrderByDescending(c => c.timestamp)
                .ToList();

            var userCache = new Dictionary<string, UserEntity>();
            foreach (var conv in conversations)
            {
                var user = await _userRepository.GetUserByIdAsync(conv.otherUserId);
                if (user != null) userCache[conv.otherUserId] = user;
            }

            return Ok(conversations.Select(c => new {
                c.chatRoom,
                c.otherUserId,
                otherUserName = userCache.GetValueOrDefault(c.otherUserId)?.Name ?? "Unknown",
                profileImageUrl = userCache.GetValueOrDefault(c.otherUserId)?.ProfileImageUrl ?? "",
                c.lastMessage,
                c.lastSenderId,
                c.timestamp,
                c.unreadCount
            }).ToList());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to get conversations: {ex.Message}");
        }
    }
}

public class StartChatRequest
{
    public string OtherUserId { get; set; } = string.Empty;
}

public class SendMessageRequest
{
    public string ReceiverId { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty; // Frontend compatibility
    public string Message { get; set; } = string.Empty;
    public string? AboutPostId { get; set; } // sent from a post's card (ChatHistory.AboutAsync)

    // Property to get the actual recipient ID from either field
    public string GetRecipientId() => !string.IsNullOrEmpty(RecipientId) ? RecipientId : ReceiverId;
}

public class MarkStatusRequest
{
    public string ChatRoom { get; set; } = string.Empty;
}

class ConversationData
{
    public string ChatRoom { get; set; } = string.Empty;
    public string OtherUserId { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public string LastSenderName { get; set; } = string.Empty;
    public string LastSenderId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string LastMessageStatus { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
}