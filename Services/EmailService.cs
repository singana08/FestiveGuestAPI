using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
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
    Task SendGuestPostNotificationAsync(string hostEmail, string hostName, string guestName, string postTitle, string location, string postId, bool isUpdate = false, string? guideSummary = null);
    Task SendUserReportAsync(UserReportEntity report);
    Task SendHostPostNotificationAsync(string guestEmail, string guestName, string hostName, string postTitle, string location, string postId, bool isUpdate = false);
    Task<EmailResponse> SendInvitationsAsync(SendInvitationsRequest request);
    Task SendSecurityAlertAsync(string email, string name, string what, string device);
    Task SendNewMessageEmailAsync(string email, string name, string senderName);
    Task SendFeedbackAsync(FeedbackEntity feedback);
    Task SendAccountStatusEmailAsync(string email, string name, bool suspended);
    Task SendStayRequestAsync(string email, string name, string fromName, string kind, string dates);
    Task SendStayConfirmedAsync(string email, string name, string otherName, string kind, string dates);
    Task SendReviewReminderAsync(string email, string name, string otherName, string kind, string otherProfileUrl);
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
            var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
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
            var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
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
            var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
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

    public async Task SendGuestPostNotificationAsync(string hostEmail, string hostName, string guestName, string postTitle, string location, string postId, bool isUpdate = false, string? guideSummary = null)
    {
        var postsUrl = $"{_secrets.AppBaseUrl}/posts";
        var label = isUpdate ? "updated their" : "new";
        var needsGuide = !string.IsNullOrEmpty(guideSummary);
        var emoji = isUpdate ? "✏️" : needsGuide ? "🧭" : "🏠";
        var heading = isUpdate ? "Guest Post Updated in Your Area!" : needsGuide ? "A Traveller Needs a Local Guide!" : "New Guest in Your Area!";
        var subject = isUpdate
            ? $"Guest Post Updated in {location} - Festive Guest"
            : needsGuide
                ? $"Local Guide Needed in {location} - Festive Guest"
                : $"New Guest Looking for Host in {location} - Festive Guest";
        var lookingFor = needsGuide ? "a local to guide them for a few hours" : "a host";
        // Names and titles are typed by members, so they're HTML-encoded before going into the email.
        var guideLine = needsGuide ? $"<p style=\"margin: 5px 0 0 0;\">🧭 {WebUtility.HtmlEncode(guideSummary)}</p>" : "";
        var content = $@"
            <p>Hi <strong>{WebUtility.HtmlEncode(hostName)}</strong>,</p>
            <p>A guest has {label} post looking for {lookingFor} in your area!</p>
            <div class=""welcome-box"">
                <h3 style=""margin: 0;"">{emoji} {WebUtility.HtmlEncode(postTitle)}</h3>
                <p style=""margin: 5px 0 0 0;"">📍 {WebUtility.HtmlEncode(location)} &bull; Posted by {WebUtility.HtmlEncode(guestName)}</p>
                {guideLine}
            </div>
            <p style=""text-align: center;"">
                <a href=""{postsUrl}"" style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;"">View Posts</a>
            </p>
            <p style=""font-size: 13px; color: #888;"">You received this because you host in {location}. Manage your notification preferences in the app settings.</p>
        ";
        await SendEmailAsync(hostEmail, subject, CreateEmailTemplate(heading, content));
    }

    public async Task SendHostPostNotificationAsync(string guestEmail, string guestName, string hostName, string postTitle, string location, string postId, bool isUpdate = false)
    {
        var postsUrl = $"{_secrets.AppBaseUrl}/posts";
        var label = isUpdate ? "updated their" : "new";
        var emoji = isUpdate ? "✏️" : "🎉";
        var heading = isUpdate ? "Host Post Updated in Your Area!" : "New Host in Your Area!";
        var subject = isUpdate
            ? $"Host Post Updated in {location} - Festive Guest"
            : $"New Host Available in {location} - Festive Guest";
        var content = $@"
            <p>Hi <strong>{WebUtility.HtmlEncode(guestName)}</strong>,</p>
            <p>A host has {label} post in your area!</p>
            <div class=""welcome-box"">
                <h3 style=""margin: 0;"">{emoji} {WebUtility.HtmlEncode(postTitle)}</h3>
                <p style=""margin: 5px 0 0 0;"">📍 {WebUtility.HtmlEncode(location)} &bull; Hosted by {WebUtility.HtmlEncode(hostName)}</p>
            </div>
            <p style=""text-align: center;"">
                <a href=""{postsUrl}"" style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;"">View Posts</a>
            </p>
            <p style=""font-size: 13px; color: #888;"">You received this because your location matches. Manage your notification preferences in the app settings.</p>
        ";
        await SendEmailAsync(guestEmail, subject, CreateEmailTemplate(heading, content));
    }

    public async Task<EmailResponse> SendInvitationsAsync(SendInvitationsRequest request)
    {
        var sent = 0;
        var failed = 0;
        var registerUrl = $"{_secrets.AppBaseUrl}/?register=true&ref={Uri.EscapeDataString(request.ReferralCode ?? "")}";

        foreach (var email in request.Emails.Distinct())
        {
            try
            {
                var content = $@"
                    <p>Hi there!</p>
                    <p><strong>{WebUtility.HtmlEncode(request.InviterName)}</strong> has invited you to join <strong>Festive Guest</strong> — India's platform for meaningful stays during festivals, business trips, and life events.</p>
                    <div class=""welcome-box"">
                        🎉 Connect with verified local hosts across 20+ states
                    </div>
                    <p>Use their personal referral link to sign up — it's completely free!</p>
                    <p style=""text-align: center;"">
                        <a href=""{registerUrl}"" style=""background: linear-gradient(135deg, #FF6B35 0%, #E55A2B 100%); color: white; padding: 14px 36px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block; font-size: 16px;"">Join Festive Guest</a>
                    </p>
                    <p style=""font-size: 13px; color: #888;"">Referral code: <strong>{WebUtility.HtmlEncode(request.ReferralCode)}</strong></p>
                ";
                await SendEmailAsync(email, $"{request.InviterName} invited you to Festive Guest!", CreateEmailTemplate("You're Invited! 🎉", content));
                sent++;
            }
            catch
            {
                failed++;
            }
        }

        return new EmailResponse
        {
            Success = sent > 0,
            Message = failed == 0
                ? $"Invitations sent to {sent} contacts"
                : $"Sent {sent}, failed {failed}"
        };
    }

    public async Task SendUserReportAsync(UserReportEntity report)
    {
        string E(string v) => WebUtility.HtmlEncode(v);
        var details = E(report.Details).Replace("\n", "<br/>");
        var content = $@"
            <p>A member has reported another member.</p>
            <div class=""security-note"">
                <p style=""margin: 0;""><strong>Reported:</strong> {E(report.ReportedName)} ({E(report.ReportedUserId)})</p>
                <p style=""margin: 5px 0 0 0;""><strong>Reason:</strong> {E(report.Reason)}</p>
                <p style=""margin: 5px 0 0 0;""><strong>Where:</strong> {E(report.Context)}</p>
                <p style=""margin: 5px 0 0 0;""><strong>By:</strong> {E(report.ReporterName)} ({E(report.ReporterId)})</p>
            </div>
            <p><strong>Details:</strong><br/>{details}</p>
            <p style=""font-size: 13px; color: #888;"">Report id {E(report.RowKey)} — stored in the UserReports table.</p>";
        await SendEmailAsync(_secrets.FromEmailAddress, $"Member report: {report.Reason} - Festive Guest", CreateEmailTemplate("Member Report", content));
    }

    private const string ManageEmails = "You can turn these emails on or off in your Profile → Edit profile.";

    public async Task SendSecurityAlertAsync(string email, string name, string what, string device)
    {
        // India has no daylight saving, so IST is a fixed +5:30.
        var when = DateTime.UtcNow.AddHours(5.5).ToString("d MMM yyyy, h:mm tt", System.Globalization.CultureInfo.InvariantCulture) + " IST";
        var content = $@"
            <p>Hi <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>
            <p>{WebUtility.HtmlEncode(what)} on your FestiveGuest account.</p>
            <div class=""security-note"">
                <p style=""margin: 0;""><strong>Device:</strong> {WebUtility.HtmlEncode(device)}</p>
                <p style=""margin: 5px 0 0 0;""><strong>When:</strong> {when}</p>
            </div>
            <p>If this was you, there's nothing to do.</p>
            <p><strong>If it wasn't you</strong>, reset your password now from the <a href=""{_secrets.AppBaseUrl}/login"">sign-in page</a> (""Forgot password"") and email customer-support@festiveguest.com.</p>
            <p style=""font-size: 13px; color: #888;"">{ManageEmails}</p>";
        await SendEmailAsync(email, $"{what} - Festive Guest", CreateEmailTemplate(what, content));
    }

    public async Task SendNewMessageEmailAsync(string email, string name, string senderName)
    {
        // The message itself stays in the app: email is less private, and links in it could be used for scams.
        var content = $@"
            <p>Hi <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>
            <p><strong>{WebUtility.HtmlEncode(senderName)}</strong> sent you a message on FestiveGuest.</p>
            <p style=""text-align: center;"">
                <a href=""{_secrets.AppBaseUrl}/chats"" style=""background: linear-gradient(135deg, #FF6B35 0%, #E55A2B 100%); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;"">Read and reply</a>
            </p>
            <p style=""font-size: 13px; color: #555;"">Stay safe: keep chatting on FestiveGuest, never send money in advance, and meet in a public place first.</p>
            <p style=""font-size: 13px; color: #888;"">{ManageEmails}</p>";
        await SendEmailAsync(email, $"{senderName} sent you a message - Festive Guest", CreateEmailTemplate("You have a new message", content));
    }

    public async Task SendFeedbackAsync(FeedbackEntity feedback)
    {
        string E(string v) => WebUtility.HtmlEncode(v);
        var content = $@"
            <p>A member sent feedback from the Help page. Reply to this email to answer them.</p>
            <div class=""security-note"">
                <p style=""margin: 0;""><strong>From:</strong> {E(feedback.Name)} ({E(feedback.Email)})</p>
                <p style=""margin: 5px 0 0 0;""><strong>Role:</strong> {E(feedback.UserType)} &bull; member id {E(feedback.UserId)}</p>
            </div>
            <p>{E(feedback.Message).Replace("\n", "<br/>")}</p>";
        await SendEmailAsync(_secrets.FromEmailAddress, $"Feedback from {feedback.Name} - Festive Guest", CreateEmailTemplate("Member Feedback", content), replyTo: feedback.Email);
    }

    public async Task SendAccountStatusEmailAsync(string email, string name, bool suspended)
    {
        var hi = $"<p>Hi <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>";
        var (subject, heading, content) = suspended
            ? ("Your account has been suspended - Festive Guest", "Your account has been suspended", $@"
                {hi}
                <p>Your FestiveGuest account has been suspended after a review by our team.</p>
                <div class=""security-note"">
                    <p style=""margin: 0;"">While it's suspended you can't sign in or send messages, and your posts are hidden from other members.</p>
                </div>
                <p>If you think this is a mistake, email <a href=""mailto:customer-support@festiveguest.com"">customer-support@festiveguest.com</a> and we'll look at it again.</p>")
            : ("Your account is active again - Festive Guest", "Your account is active again", $@"
                {hi}
                <p>Your FestiveGuest account is active again. You can sign in, message members, and your posts are visible again.</p>
                <p>Please follow our <a href=""{_secrets.AppBaseUrl}/safety-guidelines"">Safety Guidelines</a> so everyone can keep using FestiveGuest safely.</p>
                <p style=""text-align: center;"">
                    <a href=""{_secrets.AppBaseUrl}/login"" style=""background: linear-gradient(135deg, #FF6B35 0%, #E55A2B 100%); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;"">Sign in</a>
                </p>");
        await SendEmailAsync(email, subject, CreateEmailTemplate(heading, content));
    }

    private string Button(string text, string url) =>
        $@"<p style=""text-align: center;""><a href=""{url}"" style=""background: linear-gradient(135deg, #FF6B35 0%, #E55A2B 100%); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;"">{text}</a></p>";

    private static string What(string kind) => kind == "meetup" ? "meet-up" : "stay";

    public async Task SendStayRequestAsync(string email, string name, string fromName, string kind, string dates)
    {
        string E(string v) => WebUtility.HtmlEncode(v);
        var content = $@"
            <p>Hi <strong>{E(name)}</strong>,</p>
            <p><strong>{E(fromName)}</strong> asked to confirm a {What(kind)} with you:</p>
            <div class=""security-note""><p style=""margin: 0;"">📅 <strong>{E(dates)}</strong></p></div>
            <p>Open your chat with {E(fromName)} to accept or decline. Once the {What(kind)} is over, you can both leave a review.</p>
            {Button("Open chats", $"{_secrets.AppBaseUrl}/chats")}";
        await SendEmailAsync(email, $"{fromName} wants to confirm a {What(kind)} - Festive Guest", CreateEmailTemplate($"Confirm your {What(kind)}?", content));
    }

    public async Task SendStayConfirmedAsync(string email, string name, string otherName, string kind, string dates)
    {
        string E(string v) => WebUtility.HtmlEncode(v);
        var content = $@"
            <p>Hi <strong>{E(name)}</strong>,</p>
            <p><strong>{E(otherName)}</strong> confirmed your {What(kind)}:</p>
            <div class=""security-note""><p style=""margin: 0;"">📅 <strong>{E(dates)}</strong></p></div>
            <p style=""font-size: 13px; color: #555;"">Stay safe: share your plans with family (use “Share my stay” on their profile), meet in a public place first, and never pay in advance outside FestiveGuest.</p>
            {Button("Open chats", $"{_secrets.AppBaseUrl}/chats")}";
        await SendEmailAsync(email, $"{otherName} confirmed your {What(kind)} - Festive Guest", CreateEmailTemplate($"Your {What(kind)} is confirmed", content));
    }

    public async Task SendReviewReminderAsync(string email, string name, string otherName, string kind, string otherProfileUrl)
    {
        string E(string v) => WebUtility.HtmlEncode(v);
        var content = $@"
            <p>Hi <strong>{E(name)}</strong>,</p>
            <p>How was your {What(kind)} with <strong>{E(otherName)}</strong>? A few honest words help other members choose who to stay with.</p>
            <p style=""font-size: 13px; color: #555;"">Your review stays hidden until {E(otherName)} reviews you too, or for 14 days, so neither of you sees the other’s first.</p>
            {Button("Write a review", otherProfileUrl)}";
        await SendEmailAsync(email, $"How was your {What(kind)} with {otherName}? - Festive Guest", CreateEmailTemplate("Leave a review", content));
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body, string? replyTo = null)
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
        if (!string.IsNullOrEmpty(replyTo)) message.ReplyToList.Add(replyTo);
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