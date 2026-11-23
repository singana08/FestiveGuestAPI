using Azure;
using Azure.Data.Tables;
using System.Runtime.Serialization;
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

        // Filled in per request (poster's phone verified); not stored.
        [IgnoreDataMember]
        public bool UserVerified { get; set; }
        [IgnoreDataMember]
        public PosterInfo? Poster { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        private static readonly Dictionary<string, string> HelpLabels = new()
        {
            ["hospital_visit"] = "Hospital / clinic visit", ["govt_office"] = "Government office", ["exam_centre"] = "Exam centre",
            ["interview"] = "Interview", ["court"] = "Court", ["university_admission"] = "University / admission",
            ["bank_paperwork"] = "Bank paperwork", ["translation"] = "Translation", ["form_filling"] = "Form filling",
            ["city_orientation"] = "City orientation", ["elderly_companion"] = "Elderly companion",
        };

        /// <summary>e.g. "Hospital / clinic visit, Translation · 3h · ₹300/hr" — null when only a stay is needed.</summary>
        public string? GuideSummary()
        {
            if (NeedType != "guide" && NeedType != "both") return null;
            var help = string.Join(", ", GuideHelpWith.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(k => HelpLabels.GetValueOrDefault(k, k)));
            var parts = new List<string> { help };
            if (GuideHours is int h) parts.Add($"{h}h");
            parts.Add(GuideBudgetPerHour is int b ? $"₹{b}/hr" : "budget to discuss");
            return string.Join(" · ", parts.Where(p => p.Length > 0));
        }

        public List<string> GetFacilitiesList()
        {
            return string.IsNullOrEmpty(Facilities) 
                ? new List<string>() 
                : Facilities.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}