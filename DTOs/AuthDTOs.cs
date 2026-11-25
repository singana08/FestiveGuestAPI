using System.ComponentModel.DataAnnotations;

namespace FestiveGuestAPI.DTOs;

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
    
    [Required, MinLength(2)]
    public string Name { get; set; } = string.Empty;
    
    [Required, Phone]
    public string Phone { get; set; } = string.Empty;
    
    [Required]
    public string UserType { get; set; } = string.Empty; // Guest/Host
    
    public string Location { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string? ReferredBy { get; set; }
    public string? HostingAreas { get; set; } // JSON string for hosting areas

    // From POST api/phone-otp/verify (purpose=register). Only required once phone OTP is configured.
    public string? PhoneVerificationToken { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class PhoneLoginRequest
{
    [Required, Phone]
    public string Phone { get; set; } = string.Empty;

    // From POST api/phone-otp/verify (purpose=login)
    [Required]
    public string VerificationToken { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public string? Bio { get; set; }
    public string? HostingAreas { get; set; }
    public NotificationPreferencesDto? NotificationPreferences { get; set; }
}

public class ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required, StringLength(6, MinimumLength = 6)]
    public string OtpCode { get; set; } = string.Empty;
    
    [Required, MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}

public class ResetPasswordPhoneRequest
{
    [Required, Phone]
    public string Phone { get; set; } = string.Empty;

    // From POST api/phone-otp/verify (purpose=reset)
    [Required]
    public string VerificationToken { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;
    
    [Required, MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}

public class GoogleLoginRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}

public class GoogleVerifyResponse
{
    public bool Success { get; set; }
    public bool AlreadyRegistered { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserDto? User { get; set; }
    public string? Token { get; set; }
    public bool IsNewGoogleUser { get; set; }
}

public class UserDto
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty; // always masked (PhoneMask)
    public string UserType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string ProfileImageUrl { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public bool AdminVerified { get; set; } // "Verified by FestiveGuest"
    public DateTime CreatedDate { get; set; }
    public string ReferralCode { get; set; } = string.Empty;
    public string ReferredBy { get; set; } = string.Empty;
    public List<HostingAreaDto>? HostingAreas { get; set; }
    public string SubscriptionStatus { get; set; } = "free";
    public int SuccessfulReferrals { get; set; } = 0;
    public int ReferralPoints { get; set; } = 0;
    public NotificationPreferencesDto NotificationPreferences { get; set; } = new();
}

public class HostingAreaDto
{
    public string State { get; set; } = string.Empty;
    public List<string> Cities { get; set; } = new();
}

public class NotificationPreferencesDto
{
    public bool Email { get; set; } = true;    // new posts in my area
    public bool Push { get; set; } = false;
    public bool Messages { get; set; } = true; // someone messaged me
    public bool SignIns { get; set; } = true;  // new sign-in or password change on my account

    private static readonly System.Text.Json.JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Reads the stored JSON; anything missing or unreadable falls back to the defaults above.</summary>
    public static NotificationPreferencesDto Parse(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new();
        try { return System.Text.Json.JsonSerializer.Deserialize<NotificationPreferencesDto>(json, Options) ?? new(); }
        catch (System.Text.Json.JsonException) { return new(); }
    }
}