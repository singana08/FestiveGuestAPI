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

    public AdminController(IUserRepository userRepository, ISafetyRepository safety)
    {
        _userRepository = userRepository;
        _safety = safety;
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
            phone = u.Phone,
            userType = u.UserType,
            status = u.Status,
            isVerified = u.IsVerified,
            location = u.Location,
            createdDate = u.CreatedDate,
            referralCode = u.ReferralCode,
            successfulReferrals = string.IsNullOrEmpty(u.ReferralCode) ? 0 : joinedWithCode.GetValueOrDefault(u.ReferralCode.ToUpperInvariant()),
        }));
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

        user.Status = status;
        await _userRepository.UpdateUserAsync(user);
        AccountStatus.Forget(HttpContext.RequestServices, userId);
        return Ok(new { success = true, status });
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
