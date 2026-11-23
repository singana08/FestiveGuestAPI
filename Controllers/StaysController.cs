using System.Security.Claims;
using FestiveGuestAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FestiveGuestAPI.Controllers;

/// <summary>Confirming a stay or meet-up between a host and a traveller (rules in StayService).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StaysController : ControllerBase
{
    private readonly IStayService _stays;

    public StaysController(IStayService stays) => _stays = stays;

    private string? Me => User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("mine")]
    public async Task<IActionResult> Mine() => Me is { } me ? Ok(await _stays.MineAsync(me)) : Unauthorized();

    [HttpGet("with/{otherUserId}")]
    public async Task<IActionResult> With(string otherUserId) => Me is { } me ? Ok(await _stays.BetweenAsync(me, otherUserId)) : Unauthorized();

    [HttpPost]
    public async Task<IActionResult> Propose([FromBody] StayRequest request)
    {
        if (Me is not { } me) return Unauthorized();
        if (request.StartDate == null || request.EndDate == null) return BadRequest(new { message = "Pick the dates." });
        var (stay, error) = await _stays.RequestAsync(me, request.PartnerId ?? "", request.Kind, request.StartDate.Value, request.EndDate.Value);
        return error != null ? BadRequest(new { message = error }) : Ok(stay);
    }

    [HttpPost("{id}/accept")]
    public Task<IActionResult> Accept(string id) => Answer(id, accept: true);

    [HttpPost("{id}/decline")]
    public Task<IActionResult> Decline(string id) => Answer(id, accept: false);

    private async Task<IActionResult> Answer(string id, bool accept)
    {
        if (Me is not { } me) return Unauthorized();
        var error = await _stays.RespondAsync(me, id, accept);
        return error != null ? BadRequest(new { message = error }) : Ok(new { success = true });
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id)
    {
        if (Me is not { } me) return Unauthorized();
        var error = await _stays.CancelAsync(me, id);
        return error != null ? BadRequest(new { message = error }) : Ok(new { success = true });
    }
}

public class StayRequest
{
    public string? PartnerId { get; set; }
    public string? Kind { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
