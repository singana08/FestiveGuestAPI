using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.Services;
using FestiveGuestAPI.Configuration;

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
            isVerified = u.IsVerified,
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

        var changed = user.Status != status;
        user.Status = status;
        await _userRepository.UpdateUserAsync(user);
        AccountStatus.Forget(HttpContext.RequestServices, userId);

        // Tell the member, so a suspension isn't an unexplained sign-out. Who reported them stays private.
        // Not switchable in their profile: this is about their account, not a notification.
        var emailed = false;
        if (changed && !string.IsNullOrEmpty(user.Email))
        {
            try { await _email.SendAccountStatusEmailAsync(user.Email, user.Name, suspended: status == AccountStatus.Suspended); emailed = true; }
            catch (Exception ex) { _logger.LogWarning(ex, "Couldn't email member {UserId} about their account status", userId); }
        }
        return Ok(new { success = true, status, emailed });
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
}
