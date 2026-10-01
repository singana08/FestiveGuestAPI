using System.ComponentModel.DataAnnotations;

namespace FestiveGuestAPI.DTOs;

public class SendPhoneOtpRequest
{
    [Required, Phone]
    public string Phone { get; set; } = string.Empty;
}

public class VerifyPhoneOtpRequest
{
    [Required]
    public string SessionId { get; set; } = string.Empty;

    [Required]
    public string Otp { get; set; } = string.Empty;

    [Required, Phone]
    public string Phone { get; set; } = string.Empty;

    // One of: register, login, reset
    [Required]
    public string Purpose { get; set; } = string.Empty;
}

public class PhoneOtpResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? VerificationToken { get; set; }
}
