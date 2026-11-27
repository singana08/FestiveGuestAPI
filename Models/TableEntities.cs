using Azure;
using Azure.Data.Tables;

namespace FestiveGuestAPI.Models;

// Users table
public class UserEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string ProfileImageUrl { get; set; } = string.Empty;
    public string AcsUserId { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public DateTime CreatedDate { get; set; }
    public string ReferralCode { get; set; } = string.Empty;
    public string ReferredBy { get; set; } = string.Empty;
    public string HostingAreas { get; set; } = string.Empty; // JSON string for hosting areas
    public int ReferralPoints { get; set; } = 0;
    public string NotificationPreferences { get; set; } = "{\"email\":true,\"push\":false}"; // JSON string
    public string Gallery { get; set; } = string.Empty; // JSON [{ id, caption }], cover first (see Services/Gallery.cs)

    // Set while Status is Suspended, cleared on reactivation. Only admins see these.
    public string SuspendedReason { get; set; } = string.Empty; // a ReportReasons key
    public string SuspendedNote { get; set; } = string.Empty;   // the admin's private note
    public DateTime? SuspendedAt { get; set; }
    public string SuspendedBy { get; set; } = string.Empty;     // admin userId

    // "Verified by FestiveGuest": an admin checked who this member is (stronger than IsVerified, the phone).
    // Members see that it's verified, how and since when; the note and who did it are admin-only.
    public bool AdminVerified { get; set; }
    public string AdminVerifiedHow { get; set; } = string.Empty; // an AdminVerification.Methods key
    public DateTime? AdminVerifiedAt { get; set; }
    public string AdminVerifiedBy { get; set; } = string.Empty;
    public string AdminVerifiedNote { get; set; } = string.Empty;
}

/// <summary>How an admin checked a member before marking them "Verified by FestiveGuest".</summary>
public static class AdminVerification
{
    public static readonly IReadOnlyDictionary<string, string> Methods = new Dictionary<string, string>
    {
        ["id"] = "ID checked",
        ["video"] = "Video call",
        ["in_person"] = "Met in person",
    };
}

// OTPs table
public class OTPEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public string Email { get; set; } = string.Empty;
    public string OTPCode { get; set; } = string.Empty;
    public DateTime ExpirationTime { get; set; }
    public bool IsUsed { get; set; }
}

// EmailVerifications table
public class EmailVerificationEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public string Email { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
    public DateTime ExpirationTime { get; set; }
    public bool IsVerified { get; set; }
}

// Locations table
public class LocationEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

// ChatThreads table
public class ChatThreadEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public string ThreadId { get; set; } = string.Empty;
    public string User1Id { get; set; } = string.Empty;
    public string User2Id { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime LastMessageDate { get; set; }
}

// ChatMessages table
public class ChatMessageEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string ReceiverId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = "Sent";
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    // Set once when sent. (Timestamp is last-modified, so it moves when a message is marked read.)
    public DateTime? SentAt { get; set; }
    // Sent from a post's card: which post, "trip" or "listing", and its title at the time (see ChatHistory.AboutAsync).
    public string? AboutPostId { get; set; }
    public string? AboutKind { get; set; }
    public string? AboutTitle { get; set; }
}

/// <summary>
/// What a traveller sees on their own trip post (PostActivity): how often hosts opened it, and who messaged them about it.
/// One row per trip post: PartitionKey "trip", RowKey = the post's id.
/// </summary>
public class PostActivityEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public int Views { get; set; }                         // a total only: who opened it isn't kept
    public string ContactedBy { get; set; } = string.Empty; // member ids, comma-separated, first contact first

    public List<string> ContactIds() => ContactedBy.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
}

// Payments table
public class PaymentEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public string UpiReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedDate { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public string AdminNotes { get; set; } = string.Empty;
}

// Feedback table
public class FeedbackEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedDate { get; set; }
}

// Reviews table
public class ReviewEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public string ReviewerId { get; set; } = string.Empty;
    public string ReviewerName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string ReviewerRole { get; set; } = string.Empty; // Host or Guest, when it was written
    public string Reply { get; set; } = string.Empty;        // one public reply by the person reviewed
    public DateTime? RepliedAt { get; set; }
    // The confirmed stay this review is about. Reviews from before "Confirm stay" have none and stay hidden.
    public string StayId { get; set; } = string.Empty;
}

// Subscriptions table
public class SubscriptionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public string SubscriptionStatus { get; set; } = "free";
    public DateTime? PaymentVerifiedTimestamp { get; set; }
    public string UpdatedByAdmin { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty; // "cash" or "points"
}

// PointsTransactions table
public class PointsTransactionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public int Points { get; set; }
    public string Type { get; set; } = string.Empty; // earned, redeemed, admin_adjustment
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}