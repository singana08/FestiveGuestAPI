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
    public bool IsVerified { get; set; }
    public DateTime CreatedDate { get; set; }
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