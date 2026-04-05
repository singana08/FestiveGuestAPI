using System.Net;
using System.Net.Mail;
using FestiveGuestAPI.Configuration;
using FestiveGuestAPI.DTOs;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

public interface IEmailService
{
    Task<EmailResponse> SendOTPAsync(SendOTPRequest request);
    Task<EmailResponse> SendRegistrationOTPAsync(string email);
    Task<EmailResponse> SendForgotPasswordOTPAsync(string email);
    Task<EmailResponse> ValidateOTPAsync(ValidateOTPRequest request);
    Task SendRegistrationConfirmationAsync(string email, string name);
    Task SendNewGuestPostNotificationAsync(string hostEmail, string hostName, string guestName, string postTitle, string location, string postId);
    Task SendNewHostPostNotificationAsync(string guestEmail, string guestName, string hostName, string postTitle, string location, string postId);
}

public class EmailService : IEmailService
{
    private readonly IOTPRepository _otpRepository;
    private readonly AppSecrets _secrets;

    public EmailService(IOTPRepository otpRepository, AppSecrets secrets)
    {
        _otpRepository = otpRepository;
        _secrets = secrets;
    }

    public async Task<EmailResponse> SendOTPAsync(SendOTPRequest request)
    {
        try
        {
            var otpCode = new Random().Next(100000, 999999).ToString();
            var expirationTime = DateTime.UtcNow.AddMinutes(10);

            var otpEntity = new OTPEntity
            {
                Email = request.Email.ToLower(),
                OTPCode = otpCode,
                ExpirationTime = expirationTime,
                IsUsed = false
            };

            await _otpRepository.CreateOTPAsync(otpEntity);
            
            string htmlBody;
            string subject;
            
            if (request.Purpose.ToLower() == "password-reset")
            {
                htmlBody = CreateForgotPasswordOTPTemplate(otpCode);
                subject = "Password Reset Request - Festive Guest";
            }
            else
            {
                htmlBody = CreateRegistrationOTPTemplate(otpCode);
                subject = "Complete Your Registration - Festive Guest";
            }
            
            await SendEmailAsync(request.Email, subject, htmlBody);

            return new EmailResponse
            {
                Success = true,
                Message = "OTP sent successfully",
                ExpirationTime = expirationTime
            };
        }
        catch (Exception ex)
        {
            return new EmailResponse
            {
                Success = false,
                Message = $"Failed to send OTP: {ex.Message}"
            };
        }
    }

    public async Task<EmailResponse> SendRegistrationOTPAsync(string email)
    {
        try
        {
            var otpCode = new Random().Next(100000, 999999).ToString();
            var expirationTime = DateTime.UtcNow.AddMinutes(10);

            var otpEntity = new OTPEntity
            {
                Email = email.ToLower(),
                OTPCode = otpCode,
                ExpirationTime = expirationTime,
                IsUsed = false
            };

            await _otpRepository.CreateOTPAsync(otpEntity);
            var htmlBody = CreateRegistrationOTPTemplate(otpCode);
            
            await SendEmailAsync(email, "Complete Your Registration - Festive Guest", htmlBody);

            return new EmailResponse { Success = true, Message = "Registration OTP sent successfully", ExpirationTime = expirationTime };
        }
        catch (Exception ex)
        {
            return new EmailResponse { Success = false, Message = $"Failed to send registration OTP: {ex.Message}" };
        }
    }

    public async Task<EmailResponse> SendForgotPasswordOTPAsync(string email)
    {
        try
        {
            var otpCode = new Random().Next(100000, 999999).ToString();
            var expirationTime = DateTime.UtcNow.AddMinutes(10);

            var otpEntity = new OTPEntity
            {
                Email = email.ToLower(),
                OTPCode = otpCode,
                ExpirationTime = expirationTime,
                IsUsed = false
            };

            await _otpRepository.CreateOTPAsync(otpEntity);
            var htmlBody = CreateForgotPasswordOTPTemplate(otpCode);
            
            await SendEmailAsync(email, "Password Reset Request - Festive Guest", htmlBody);

            return new EmailResponse { Success = true, Message = "Password reset OTP sent successfully", ExpirationTime = expirationTime };
        }
        catch (Exception ex)
        {
            return new EmailResponse { Success = false, Message = $"Failed to send password reset OTP: {ex.Message}" };
        }
    }

    public async Task<EmailResponse> ValidateOTPAsync(ValidateOTPRequest request)
    {
        var validOTP = await _otpRepository.GetValidOTPAsync(request.Email.ToLower(), request.OTPCode);
        
        if (validOTP == null)
        {
            return new EmailResponse
            {
                Success = false,
                Message = "Invalid or expired OTP"
            };
        }

        await _otpRepository.MarkOTPAsUsedAsync(request.Email.ToLower(), request.OTPCode);

        return new EmailResponse
        {
            Success = true,
            Message = "OTP validated successfully"
        };
    }

    public async Task SendRegistrationConfirmationAsync(string email, string name)
    {
        var htmlBody = CreateWelcomeTemplate(name);
        await SendEmailAsync(email, "Welcome to Festive Guest!", htmlBody);
    }

    public async Task SendNewGuestPostNotificationAsync(string hostEmail, string hostName, string guestName, string postTitle, string location, string postId)
    {
        var appUrl = _secrets.AppBaseUrl;
        var content = $@"
            <p>Hi <strong>{hostName}</strong>,</p>
            <p>A new guest is looking for a host in your area!</p>
            <div class=""welcome-box"">
                <h3 style=""margin: 0;"">🏠 {postTitle}</h3>
                <p style=""margin: 5px 0 0 0;"">📍 {location} &bull; Posted by {guestName}</p>
            </div>
            <p style=""text-align: center;"">
                <a href=""{appUrl}"" style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;"">Open Festive Guest</a>
            </p>
            <p style=""font-size: 13px; color: #888;"">You received this because you host in {location}. Manage your notification preferences in the app settings.</p>
        ";
        await SendEmailAsync(hostEmail, $"New Guest Looking for Host in {location} - Festive Guest", CreateEmailTemplate("New Guest in Your Area!", content));
    }

    public async Task SendNewHostPostNotificationAsync(string guestEmail, string guestName, string hostName, string postTitle, string location, string postId)
    {
        var appUrl = _secrets.AppBaseUrl;
        var content = $@"
            <p>Hi <strong>{guestName}</strong>,</p>
            <p>A new host is available in your area!</p>
            <div class=""welcome-box"">
                <h3 style=""margin: 0;"">🎉 {postTitle}</h3>
                <p style=""margin: 5px 0 0 0;"">📍 {location} &bull; Hosted by {hostName}</p>
            </div>
            <p style=""text-align: center;"">
                <a href=""{appUrl}"" style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;"">Open Festive Guest</a>
            </p>
            <p style=""font-size: 13px; color: #888;"">You received this because your location matches. Manage your notification preferences in the app settings.</p>
        ";
        await SendEmailAsync(guestEmail, $"New Host Available in {location} - Festive Guest", CreateEmailTemplate("New Host in Your Area!", content));
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        using var client = new SmtpClient(_secrets.SmtpHost, int.Parse(_secrets.SmtpPort))
        {
            Credentials = new NetworkCredential(_secrets.SmtpUsername, _secrets.SmtpPassword),
            EnableSsl = true
        };

        var message = new MailMessage(_secrets.FromEmailAddress, toEmail, subject, body)
        {
            IsBodyHtml = true
        };
        await client.SendMailAsync(message);
    }

    private string CreateEmailTemplate(string title, string content)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: white; border-radius: 10px; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; text-align: center; color: white; }}
        .brand {{ font-size: 28px; font-weight: bold; margin: 0; }}
        .content {{ padding: 30px; }}
        .otp-code {{ background-color: #f8f9fa; border: 2px dashed #667eea; padding: 20px; text-align: center; margin: 20px 0; border-radius: 8px; }}
        .otp-number {{ font-size: 32px; font-weight: bold; color: #667eea; letter-spacing: 5px; }}
        .footer {{ background-color: #f8f9fa; padding: 20px; text-align: center; color: #666; font-size: 14px; }}
        .welcome-box {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 8px; margin: 20px 0; text-align: center; }}
        .security-note {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1 class=""brand"">Festive Guest</h1>
        </div>
        <div class=""content"">
            <h2 style=""color: #333; margin-bottom: 20px;"">{title}</h2>
            {content}
        </div>
        <div class=""footer"">
            <p>Best regards,<br>Festive Guest Team</p>
            <p style=""font-size: 12px; color: #999;"">&copy; 2024 Festive Guest. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string CreateRegistrationOTPTemplate(string otpCode)
    {
        var content = $@"
            <p>Welcome to <strong>Festive Guest</strong>!</p>
            <p>To complete your registration, please verify your email address with the code below:</p>
            <div class=""otp-code"">
                <div class=""otp-number"">{otpCode}</div>
            </div>
            <p><strong>This code will expire in 10 minutes.</strong></p>
            <p>Once verified, you'll be able to:</p>
            <ul>
                <li>Create your profile</li>
                <li>Browse and connect with members</li>
                <li>Start meaningful conversations</li>
            </ul>
        ";
        return CreateEmailTemplate("Complete Your Registration", content);
    }

    private string CreateForgotPasswordOTPTemplate(string otpCode)
    {
        var content = $@"
            <p>We received a request to reset your password for your Festive Guest account.</p>
            <div class=""security-note"">
                <p><strong>🔒 Security Code:</strong></p>
            </div>
            <div class=""otp-code"">
                <div class=""otp-number"">{otpCode}</div>
            </div>
            <p><strong>This code will expire in 10 minutes.</strong></p>
            <p>If you didn't request a password reset, please ignore this email and your password will remain unchanged.</p>
            <p>For your security, never share this code with anyone.</p>
        ";
        return CreateEmailTemplate("Password Reset Request", content);
    }

    private string CreateWelcomeTemplate(string name)
    {
        var content = $@"
            <div class=""welcome-box"">
                <h3 style=""margin: 0; font-size: 24px;"">🎉 Welcome to Festive Guest!</h3>
            </div>
            <p>Dear <strong>{name}</strong>,</p>
            <p>Congratulations! Your account has been successfully created and verified.</p>
            <p>You're now part of our growing community where meaningful connections are made.</p>
            <p><strong>What's next?</strong></p>
            <ul>
                <li>📝 Complete your profile to attract the right matches</li>
                <li>🔍 Browse through verified profiles</li>
                <li>💬 Start conversations with potential matches</li>
                <li>🔒 Enjoy secure and private messaging</li>
            </ul>
            <p>We're excited to help you find your perfect match!</p>
        ";
        return CreateEmailTemplate("Account Successfully Created!", content);
    }
}