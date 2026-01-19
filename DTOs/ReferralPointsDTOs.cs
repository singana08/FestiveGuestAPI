using System.ComponentModel.DataAnnotations;

namespace FestiveGuestAPI.DTOs;

public class RedeemPointsRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;
}

public class RedeemPointsResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int RemainingPoints { get; set; }
    public string SubscriptionStatus { get; set; } = string.Empty;
    public DateTime? SubscriptionExpiryDate { get; set; }
}

public class AdjustPointsRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public int Points { get; set; }
    
    [Required]
    public string Description { get; set; } = string.Empty;
}
