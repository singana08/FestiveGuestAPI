using Azure;
using Azure.Data.Tables;

namespace FestiveGuestAPI.Models;

/// <summary>
/// A stay (or guide meet-up) that one member proposed and the other confirmed — like a friend request,
/// but for a real stay. Reviews are only possible once a confirmed stay has ended.
/// </summary>
public class StayEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "STAY"; // ponytail: one partition; split by month if stays reach the tens of thousands
    public string RowKey { get; set; } = string.Empty;  // stay id
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string RequesterId { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string PartnerId { get; set; } = string.Empty;   // the one who accepts or declines
    public string PartnerName { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public string GuestId { get; set; } = string.Empty;
    public string Kind { get; set; } = "stay";              // stay | meetup (guide request)
    public DateTime StartDate { get; set; }                 // dates only, stored as UTC midnight
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Pending";         // Pending | Confirmed | Declined | Cancelled
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? ReminderSentAt { get; set; }           // "how was your stay?" email, sent once after it ends
    public DateTime? StartReminderSentAt { get; set; }      // "your stay starts tomorrow" email, sent once
    public string Note { get; set; } = string.Empty;        // what the requester wrote (arrival time, people…)
}
