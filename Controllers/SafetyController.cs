using System.Security.Claims;
using FestiveGuestAPI.Models;
using FestiveGuestAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FestiveGuestAPI.Controllers;

/// <summary>Reporting members to the FestiveGuest team, and blocking members.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SafetyController : ControllerBase
{
    private static readonly HashSet<string> Reasons = new() { "fake_profile", "harassment", "scam", "inappropriate", "safety", "other" };

    private readonly ISafetyRepository _safety;
    private readonly IUserRepository _users;
    private readonly IEmailService _email;
    private readonly ILogger<SafetyController> _logger;

    public SafetyController(ISafetyRepository safety, IUserRepository users, IEmailService email, ILogger<SafetyController> logger)
    {
        _safety = safety;
        _users = users;
        _email = email;
        _logger = logger;
    }

    private string? Me => User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpPost("report")]
    public async Task<IActionResult> Report([FromBody] ReportRequest request)
    {
        var me = Me;
        if (string.IsNullOrEmpty(me)) return Unauthorized();

        var reason = (request.Reason ?? "").Trim().ToLowerInvariant();
        if (!Reasons.Contains(reason)) return BadRequest(new { message = "Pick a reason for the report" });
        var details = (request.Details ?? "").Trim();
        if (details.Length > 1000) return BadRequest(new { message = "Please keep the details under 1000 characters" });
        if (string.IsNullOrWhiteSpace(request.ReportedUserId) || request.ReportedUserId == me)
            return BadRequest(new { message = "You can't report this member" });

        var reported = await _users.GetUserByIdAsync(request.ReportedUserId);
        if (reported == null) return NotFound(new { message = "Member not found" });
        var reporter = await _users.GetUserByIdAsync(me);

        var context = (request.Context ?? "").Trim();
        var report = new UserReportEntity
        {
            RowKey = $"{DateTime.UtcNow.Ticks}_{Guid.NewGuid().ToString("N")[..8]}",
            ReporterId = me,
            ReporterName = reporter?.Name ?? "",
            ReportedUserId = reported.RowKey,
            ReportedName = reported.Name,
            Reason = reason,
            Details = details,
            Context = context.Length > 100 ? context[..100] : context,
            CreatedAt = DateTime.UtcNow,
        };
        await _safety.AddReportAsync(report);

        // The report is saved either way; the email is a heads-up for the team.
        try { await _email.SendUserReportAsync(report); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to email report {ReportId}", report.RowKey); }

        return Ok(new { success = true });
    }

    [HttpGet("blocks")]
    public async Task<IActionResult> GetBlocks()
    {
        var me = Me;
        if (string.IsNullOrEmpty(me)) return Unauthorized();
        var blocks = await _safety.GetBlocksAsync(me);
        return Ok(blocks.Select(b => new { userId = b.RowKey, name = b.BlockedName, blockedAt = b.CreatedAt }));
    }

    [HttpPost("block/{userId}")]
    public async Task<IActionResult> Block(string userId)
    {
        var me = Me;
        if (string.IsNullOrEmpty(me)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(userId) || userId == me) return BadRequest(new { message = "You can't block this member" });

        var other = await _users.GetUserByIdAsync(userId);
        if (other == null) return NotFound(new { message = "Member not found" });

        await _safety.BlockAsync(new UserBlockEntity { PartitionKey = me, RowKey = other.RowKey, BlockedName = other.Name, CreatedAt = DateTime.UtcNow });
        return Ok(new { success = true });
    }

    [HttpDelete("block/{userId}")]
    public async Task<IActionResult> Unblock(string userId)
    {
        var me = Me;
        if (string.IsNullOrEmpty(me)) return Unauthorized();
        await _safety.UnblockAsync(me, userId);
        return Ok(new { success = true });
    }
}

public class ReportRequest
{
    public string? ReportedUserId { get; set; }
    public string? Reason { get; set; }
    public string? Details { get; set; }
    public string? Context { get; set; }
}
