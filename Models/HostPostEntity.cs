using Azure;
using Azure.Data.Tables;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

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
        // Stored for notifications; never sent to other members in API responses.
        [JsonIgnore]
        public string UserEmail { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Location { get; set; } = "";
        public string Amenities { get; set; } = "";
        public int? MaxGuests { get; set; }
        public decimal? PricePerNight { get; set; }
        public DateTime? CommencementDate { get; set; }
        public string Status { get; set; } = "Active";

        // Filled in per request (poster's phone verified); not stored.
        [IgnoreDataMember]
        public bool UserVerified { get; set; }
        [IgnoreDataMember]
        public PosterInfo? Poster { get; set; }
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