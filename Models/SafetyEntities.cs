using Azure;
using Azure.Data.Tables;

namespace FestiveGuestAPI.Models;

// UserBlocks table: one row per (blocker, blocked) pair.
public class UserBlockEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // the member who blocked
    public string RowKey { get; set; } = string.Empty;       // the member who is blocked
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string BlockedName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// UserReports table: a member reporting another member to the FestiveGuest team.
public class UserReportEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "Report";
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string ReporterId { get; set; } = string.Empty;
    public string ReporterName { get; set; } = string.Empty;
    public string ReportedUserId { get; set; } = string.Empty;
    public string ReportedName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;   // fake_profile | harassment | scam | inappropriate | safety | other
    public string Details { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;  // where it happened, e.g. "chat" or "profile"
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; }
}
