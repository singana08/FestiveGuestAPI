using Azure;
using Azure.Data.Tables;

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
        public string UserEmail { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Location { get; set; } = "";
        public string Facilities { get; set; } = "";
        public int? Visitors { get; set; }
        public int? Days { get; set; }
        public string Status { get; set; } = "Active";
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