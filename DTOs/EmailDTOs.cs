using System.ComponentModel.DataAnnotations;

namespace FestiveGuestAPI.DTOs;

public class SendOTPRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string Purpose { get; set; } = string.Empty; // Registration, PasswordReset, etc.
}

public class ValidateOTPRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required, StringLength(6, MinimumLength = 6)]
    public string OTPCode { get; set; } = string.Empty;
}

public class EmailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? ExpirationTime { get; set; }
}