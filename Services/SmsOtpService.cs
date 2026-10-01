using FestiveGuestAPI.Configuration;
using FestiveGuestAPI.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace FestiveGuestAPI.Services;

public interface ISmsOtpService
{
    bool IsConfigured { get; }
    Task<PhoneOtpResponse> SendOtpAsync(string phone);
    Task<PhoneOtpResponse> VerifyOtpAsync(string sessionId, string otp, string phone, string purpose);
    (string Phone, string Purpose)? ValidateVerificationToken(string? token, string expectedPhone, string expectedPurpose);
}

// Wraps 2Factor.in's SMS OTP API. 2Factor holds the OTP/session state itself (no local table needed) —
// a successful verify here just earns the caller a short-lived, purpose-scoped JWT that proves the phone
// was verified, which Register/LoginWithPhone/ResetPasswordWithPhone then check instead of trusting the client.
public class SmsOtpService : ISmsOtpService
{
    private const string VerifyIssuer = "FestiveGuestAPI.PhoneVerify";
    private const string VerifyAudience = "FestiveGuestApp.PhoneVerify";

    private readonly AppSecrets _secrets;
    private readonly IHttpClientFactory _httpClientFactory;

    public SmsOtpService(AppSecrets secrets, IHttpClientFactory httpClientFactory)
    {
        _secrets = secrets;
        _httpClientFactory = httpClientFactory;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_secrets.TwoFactorApiKey);

    public async Task<PhoneOtpResponse> SendOtpAsync(string phone)
    {
        if (!IsConfigured)
            return new PhoneOtpResponse { Success = false, Message = "Phone OTP is not configured yet." };

        try
        {
            using var http = _httpClientFactory.CreateClient();
            // Plain AUTOGEN uses 2Factor's own pre-approved default template — no dashboard
            // template/Sender ID setup required. (AUTOGEN/{template_name} is the variant that needs one.)
            var url = $"https://2factor.in/API/V1/{_secrets.TwoFactorApiKey}/SMS/{Uri.EscapeDataString(phone)}/AUTOGEN";
            var response = await http.GetAsync(url);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            var status = doc.RootElement.TryGetProperty("Status", out var s) ? s.GetString() : null;
            var details = doc.RootElement.TryGetProperty("Details", out var d) ? d.GetString() : null;
            if (status != "Success" || string.IsNullOrEmpty(details))
                return new PhoneOtpResponse { Success = false, Message = details ?? "Failed to send OTP." };

            return new PhoneOtpResponse { Success = true, SessionId = details, Message = "OTP sent." };
        }
        catch (Exception ex)
        {
            return new PhoneOtpResponse { Success = false, Message = $"Failed to send OTP: {ex.Message}" };
        }
    }

    public async Task<PhoneOtpResponse> VerifyOtpAsync(string sessionId, string otp, string phone, string purpose)
    {
        if (!IsConfigured)
            return new PhoneOtpResponse { Success = false, Message = "Phone OTP is not configured yet." };

        try
        {
            using var http = _httpClientFactory.CreateClient();
            var url = $"https://2factor.in/API/V1/{_secrets.TwoFactorApiKey}/SMS/VERIFY/{Uri.EscapeDataString(sessionId)}/{Uri.EscapeDataString(otp)}";
            var response = await http.GetAsync(url);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            var status = doc.RootElement.TryGetProperty("Status", out var s) ? s.GetString() : null;
            var details = doc.RootElement.TryGetProperty("Details", out var d) ? d.GetString() : null;
            if (status != "Success" || details != "OTP Matched")
                return new PhoneOtpResponse { Success = false, Message = "Invalid or expired OTP." };

            return new PhoneOtpResponse { Success = true, VerificationToken = IssueVerificationToken(phone, purpose), Message = "OTP verified." };
        }
        catch (Exception ex)
        {
            return new PhoneOtpResponse { Success = false, Message = $"Verification failed: {ex.Message}" };
        }
    }

    private string IssueVerificationToken(string phone, string purpose)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_secrets.JwtSecretKey);
        var claims = new List<Claim>
        {
            new Claim("phone", phone),
            new Claim("purpose", purpose)
        };
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            Issuer = VerifyIssuer,
            Audience = VerifyAudience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public (string Phone, string Purpose)? ValidateVerificationToken(string? token, string expectedPhone, string expectedPurpose)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_secrets.JwtSecretKey);
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = VerifyIssuer,
                ValidateAudience = true,
                ValidAudience = VerifyAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var phone = principal.FindFirst("phone")?.Value;
            var purpose = principal.FindFirst("purpose")?.Value;
            if (phone != expectedPhone || purpose != expectedPurpose)
                return null;

            return (phone, purpose);
        }
        catch
        {
            return null;
        }
    }
}
