using Azure;
using Azure.Data.Tables;
using System.Text.Json.Serialization;

namespace FestiveGuestAPI.Models
{
    public class GuestPostEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = "";
        public string RowKey { get; set; } = "";
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        // Stored for notifications; never sent to other members in API responses.
        [JsonIgnore]
        public string UserEmail { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Location { get; set; } = "";
        public string Facilities { get; set; } = "";
        public int? Visitors { get; set; }
        public int? Days { get; set; }
        public DateTime? VisitingDate { get; set; }
        public string Status { get; set; } = "Active";

        // What the traveller needs: "stay", "guide" (a local for a few hours) or "both".
        // Posts created before this field existed read back as "stay".
        public string NeedType { get; set; } = "stay";
        public string GuideHelpWith { get; set; } = "";   // comma-separated keys, e.g. "hospital_visit,translation"
        public int? GuideHours { get; set; }
        public string GuideLanguages { get; set; } = "";  // comma-separated
        public int? GuideBudgetPerHour { get; set; }      // null = to be discussed
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public List<string> GetFacilitiesList()
        {
            return string.IsNullOrEmpty(Facilities) 
                ? new List<string>() 
                : Facilities.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}