using System.ComponentModel.DataAnnotations;

namespace FestiveGuestAPI.DTOs;

public class SubscriptionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SubscriptionDto? Subscription { get; set; }
}

public class SubscriptionDto
{
    public string UserId { get; set; } = string.Empty;
    public string SubscriptionStatus { get; set; } = string.Empty;
    public DateTime? PaymentVerifiedTimestamp { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public class UpdateSubscriptionRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [RegularExpression("^(free|pending|paid)$", ErrorMessage = "Status must be free, pending, or paid")]
    public string SubscriptionStatus { get; set; } = string.Empty;
    
    public DateTime? PaymentVerifiedTimestamp { get; set; }
    
    public string PaymentMethod { get; set; } = "cash"; // "cash" or "points"
}
