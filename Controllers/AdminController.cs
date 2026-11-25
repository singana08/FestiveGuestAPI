using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.Services;
using FestiveGuestAPI.Configuration;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Controllers;

/// <summary>Members and reports, for the FestiveGuest team.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[AdminAuthorize]
public class AdminController : ControllerBase
{
    private static readonly HashSet<string> MemberStatuses = new() { "Active", AccountStatus.Suspended };
    private static readonly HashSet<string> ReportStatuses = new() { "Open", "Resolved" };

    private readonly IUserRepository _userRepository;
    private readonly ISafetyRepository _safety;
    private readonly IEmailService _email;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IUserRepository userRepository, ISafetyRepository safety, IEmailService email, ILogger<AdminController> logger)
    {
        _userRepository = userRepository;
        _safety = safety;
        _email = email;
        _logger = logger;
    }

    private string? Me => User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = (await _userRepository.GetAllUsersAsync()).ToList();
        var joinedWithCode = users
            .Where(u => !string.IsNullOrEmpty(u.ReferredBy) && u.Status == "Active")
            .GroupBy(u => u.ReferredBy.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Count());

        return Ok(users.Select(u => new
        {
            userId = u.RowKey,
            name = u.Name,
            email = u.Email,
            maskedPhone = PhoneMask.Apply(u.Phone),
            userType = u.UserType,
            status = u.Status,
            suspendedReason = u.SuspendedReason,
            suspendedNote = u.SuspendedNote,
            suspendedAt = u.SuspendedAt,
            isVerified = u.IsVerified,
            adminVerified = u.AdminVerified,
            adminVerifiedHow = u.AdminVerifiedHow,
            adminVerifiedAt = u.AdminVerifiedAt,
            adminVerifiedNote = u.AdminVerifiedNote,
            location = u.Location,
            createdDate = u.CreatedDate,
            referralCode = u.ReferralCode,
            successfulReferrals = string.IsNullOrEmpty(u.ReferralCode) ? 0 : joinedWithCode.GetValueOrDefault(u.ReferralCode.ToUpperInvariant()),
        }));
    }

    /// <summary>
    /// The only place a full phone number leaves the API: an admin opening one member to call or WhatsApp them.
    /// Each view is written to the server log (Application Insights).
    /// </summary>
    [HttpGet("users/{userId}/phone")]
    public async Task<IActionResult> GetPhone(string userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null) return NotFound(new { message = "Member not found." });
        _logger.LogInformation("Admin {AdminId} viewed the phone number of member {UserId}", Me, userId);
        return Ok(new { phone = user.Phone ?? "", isVerified = user.IsVerified });
    }

    /// <summary>Reviews about and by a member, including ones still hidden — for handling reports.</summary>
    [HttpGet("users/{userId}/reviews")]
    public async Task<IActionResult> GetMemberReviews(string userId, [FromServices] IReviewService reviews)
    {
        var (about, by) = await reviews.AllForAdminAsync(userId);
        return Ok(new { about, by });
    }

    [HttpDelete("reviews/{revieweeId}/{reviewId}")]
    public async Task<IActionResult> DeleteReview(string revieweeId, string reviewId, [FromServices] IReviewService reviews)
    {
        if (!await reviews.DeleteAsync(revieweeId, reviewId)) return NotFound(new { message = "Review not found." });
        _logger.LogInformation("Admin {AdminId} removed review {ReviewId} about member {UserId}", Me, reviewId, revieweeId);
        return NoContent();
    }

    [HttpDelete("users/{userId}/gallery/{photoId}")]
    public async Task<IActionResult> RemovePhoto(string userId, string photoId, [FromServices] IGalleryService gallery)
    {
        if (!await gallery.RemoveAsync(userId, photoId)) return NotFound(new { message = "Photo not found." });
        _logger.LogInformation("Admin {AdminId} removed gallery photo {PhotoId} of member {UserId}", Me, photoId, userId);
        return NoContent();
    }

    /// <summary>Suspend a member (can't sign in, posts hidden, can't message) or reactivate them.</summary>
    [HttpPost("users/{userId}/status")]
    public async Task<IActionResult> SetUserStatus(string userId, [FromBody] StatusRequest request)
    {
        var status = request.Status ?? "";
        if (!MemberStatuses.Contains(status)) return BadRequest(new { message = "Status must be Active or Suspended" });
        if (userId == Me) return BadRequest(new { message = "You can't change your own account here" });

        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null) return NotFound(new { message = "Member not found" });
        if (user.UserType.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Admin accounts can't be suspended here" });

        var suspending = status == AccountStatus.Suspended;
        var reason = request.Reason ?? "";
        var note = (request.Note ?? "").Trim();
        if (suspending && !ReportReasons.Labels.ContainsKey(reason))
            return BadRequest(new { message = "Pick a reason for the suspension" });
        if (note.Length > 500) return BadRequest(new { message = "Keep the note under 500 characters" });

        var changed = user.Status != status;
        user.Status = status;
        // Why, when and by whom — for admins. Cleared on reactivation.
        user.SuspendedReason = suspending ? reason : "";
        user.SuspendedNote = suspending ? note : "";
        user.SuspendedAt = suspending ? DateTime.UtcNow : null;
        user.SuspendedBy = suspending ? Me ?? "" : "";
        await _userRepository.UpdateUserAsync(user);
        AccountStatus.Forget(HttpContext.RequestServices, userId);
        _logger.LogInformation("Admin {AdminId} set member {UserId} to {Status} (reason: {Reason})", Me, userId, status, suspending ? reason : "-");

        // Tell the member, so a suspension isn't an unexplained sign-out. They get the reason, never who reported
        // them and never the admin's note. Not switchable in their profile: this is about their account.
        var emailed = false;
        if (changed && !string.IsNullOrEmpty(user.Email))
        {
            try { await _email.SendAccountStatusEmailAsync(user.Email, user.Name, suspending, suspending ? ReportReasons.Labels[reason] : null); emailed = true; }
            catch (Exception ex) { _logger.LogWarning(ex, "Couldn't email member {UserId} about their account status", userId); }
        }
        return Ok(new { success = true, status, emailed });
    }

    /// <summary>"Verified by FestiveGuest": mark a member as checked by the team (how is required), or take it away.</summary>
    [HttpPost("users/{userId}/verify")]
    public async Task<IActionResult> SetAdminVerified(string userId, [FromBody] VerifyRequest request)
    {
        if (userId == Me) return BadRequest(new { message = "Ask another admin to verify you" });
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null) return NotFound(new { message = "Member not found" });

        var how = request.How ?? "";
        var note = (request.Note ?? "").Trim();
        if (request.Verified && !AdminVerification.Methods.ContainsKey(how))
            return BadRequest(new { message = "Say how you checked them" });
        if (note.Length > 500) return BadRequest(new { message = "Keep the note under 500 characters" });

        user.AdminVerified = request.Verified;
        user.AdminVerifiedHow = request.Verified ? how : "";
        user.AdminVerifiedNote = request.Verified ? note : "";
        user.AdminVerifiedAt = request.Verified ? DateTime.UtcNow : null;
        user.AdminVerifiedBy = request.Verified ? Me ?? "" : "";
        await _userRepository.UpdateUserAsync(user);
        _logger.LogInformation("Admin {AdminId} set Verified by FestiveGuest to {Verified} for member {UserId} ({How})", Me, request.Verified, userId, request.Verified ? how : "-");
        return Ok(new { success = true, adminVerified = user.AdminVerified, adminVerifiedHow = user.AdminVerifiedHow, adminVerifiedAt = user.AdminVerifiedAt });
    }

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports()
    {
        var reports = await _safety.GetReportsAsync();
        var statusById = (await _userRepository.GetAllUsersAsync()).ToDictionary(u => u.RowKey, u => u.Status);

        return Ok(reports.Select(r => new
        {
            id = r.RowKey,
            reason = r.Reason,
            details = r.Details,
            context = r.Context,
            status = r.Status,
            createdAt = r.CreatedAt,
            reporterId = r.ReporterId,
            reporterName = r.ReporterName,
            reportedUserId = r.ReportedUserId,
            reportedName = r.ReportedName,
            reportedStatus = statusById.GetValueOrDefault(r.ReportedUserId) ?? "Deleted",
        }));
    }

    [HttpPost("reports/{reportId}/status")]
    public async Task<IActionResult> SetReportStatus(string reportId, [FromBody] StatusRequest request)
    {
        var status = request.Status ?? "";
        if (!ReportStatuses.Contains(status)) return BadRequest(new { message = "Status must be Open or Resolved" });
        return await _safety.SetReportStatusAsync(reportId, status)
            ? Ok(new { success = true, status })
            : NotFound(new { message = "Report not found" });
    }
}

public class StatusRequest
{
    public string? Status { get; set; }
    public string? Reason { get; set; } // suspending a member: a ReportReasons key (required)
    public string? Note { get; set; }   // suspending a member: a private note for admins (optional)
}

public class VerifyRequest
{
    public bool Verified { get; set; }
    public string? How { get; set; }  // an AdminVerification.Methods key (required when verifying)
    public string? Note { get; set; } // private, for admins
}
