using Azure;
using Azure.Data.Tables;

namespace FestiveGuestAPI.Models
{
    public class HostPostEntity : ITableEntity
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
        public string Amenities { get; set; } = "";
        public int? MaxGuests { get; set; }
        public decimal? PricePerNight { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public List<string> GetAmenitiesList()
        {
            return string.IsNullOrEmpty(Amenities) 
                ? new List<string>() 
                : Amenities.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}