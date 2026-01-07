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

    public ChatController(IUserRepository userRepository, TableServiceClient tableServiceClient)
    {
        _userRepository = userRepository;
        _tableServiceClient = tableServiceClient;
    }

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
                    message = entity.Message,
                    timestamp = entity.Timestamp,
                    status = entity.Status,
                    deliveredAt = entity.DeliveredAt,
                    readAt = entity.ReadAt
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

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
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

            var conversations = new Dictionary<string, ConversationData>();
            
            await foreach (var entity in messagesTable.QueryAsync<ChatMessageEntity>())
            {
                var chatRoom = entity.PartitionKey;
                if (!chatRoom.StartsWith("chat_")) continue;
                
                var userIds = chatRoom.Replace("chat_", "").Split('_');
                if (userIds.Length != 2 || !userIds.Contains(userId)) continue;
                
                var otherUserId = userIds.First(id => id != userId);
                
                if (!conversations.ContainsKey(otherUserId))
                {
                    conversations[otherUserId] = new ConversationData
                    {
                        ChatRoom = chatRoom,
                        OtherUserId = otherUserId,
                        LastMessage = entity.Message,
                        LastSenderName = entity.SenderName,
                        LastSenderId = entity.SenderId,
                        Timestamp = entity.Timestamp ?? DateTimeOffset.MinValue,
                        LastMessageStatus = entity.Status,
                        UnreadCount = 0
                    };
                }
                else if (conversations[otherUserId].Timestamp < entity.Timestamp)
                {
                    conversations[otherUserId].LastMessage = entity.Message;
                    conversations[otherUserId].LastSenderName = entity.SenderName;
                    conversations[otherUserId].LastSenderId = entity.SenderId;
                    conversations[otherUserId].Timestamp = entity.Timestamp ?? DateTimeOffset.MinValue;
                    conversations[otherUserId].LastMessageStatus = entity.Status;
                }
                
                // Count unread messages (messages sent by other user that current user hasn't read)
                if (entity.SenderId == otherUserId && entity.Status != "Read")
                {
                    conversations[otherUserId].UnreadCount++;
                }
            }

            var result = new List<object>();
            foreach (var conv in conversations.Values.OrderByDescending(c => c.Timestamp))
            {
                var otherUser = await _userRepository.GetUserByIdAsync(conv.OtherUserId);
                result.Add(new {
                    chatRoom = conv.ChatRoom,
                    otherUserId = conv.OtherUserId,
                    otherUserName = otherUser?.Name ?? "Unknown",
                    profileImageUrl = otherUser?.ProfileImageUrl ?? "",
                    lastMessage = conv.LastMessage,
                    lastSenderName = conv.LastSenderName,
                    lastSenderId = conv.LastSenderId,
                    timestamp = conv.Timestamp,
                    status = conv.LastMessageStatus,
                    unreadCount = conv.UnreadCount
                });
            }

            return Ok(result);
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