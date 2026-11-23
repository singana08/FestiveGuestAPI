using System.Security.Claims;
using FestiveGuestAPI.Hubs;
using FestiveGuestAPI.Models;
using FestiveGuestAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace FestiveGuestAPI.Controllers;

/// <summary>Stay requests between a host and a traveller (rules in StayService). Each change is also a chat message.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StaysController : ControllerBase
{
    private readonly IStayService _stays;
    private readonly IHubContext<ChatHub> _hub;

    public StaysController(IStayService stays, IHubContext<ChatHub> hub)
    {
        _stays = stays;
        _hub = hub;
    }

    private string? Me => User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>Shows the new chat message straight away in an open chat (same event as a typed message).</summary>
    private async Task<object?> PushAsync(ChatMessageEntity? message)
    {
        if (message == null) return null;
        var payload = ChatHistory.Payload(message);
        try { await _hub.Clients.Group(message.PartitionKey).SendAsync("ReceiveMessage", payload); }
        catch { /* live push is a nicety; the message is saved and shows on the next refresh */ }
        return payload;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine() => Me is { } me ? Ok(await _stays.MineAsync(me)) : Unauthorized();

    [HttpGet("with/{otherUserId}")]
    public async Task<IActionResult> With(string otherUserId) => Me is { } me ? Ok(await _stays.BetweenAsync(me, otherUserId)) : Unauthorized();

    [HttpPost]
    public async Task<IActionResult> Propose([FromBody] StayRequest request)
    {
        if (Me is not { } me) return Unauthorized();
        if (request.StartDate == null || request.EndDate == null) return BadRequest(new { message = "Pick the dates." });
        var (stay, error, message) = await _stays.RequestAsync(me, request.PartnerId ?? "", request.Kind, request.StartDate.Value, request.EndDate.Value, request.Note);
        return error != null ? BadRequest(new { message = error }) : Ok(new { stay, message = await PushAsync(message) });
    }

    [HttpPost("{id}/accept")]
    public Task<IActionResult> Accept(string id) => Answer(id, accept: true);

    [HttpPost("{id}/decline")]
    public Task<IActionResult> Decline(string id) => Answer(id, accept: false);

    private async Task<IActionResult> Answer(string id, bool accept)
    {
        if (Me is not { } me) return Unauthorized();
        var (error, message) = await _stays.RespondAsync(me, id, accept);
        return error != null ? BadRequest(new { message = error }) : Ok(new { success = true, message = await PushAsync(message) });
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id)
    {
        if (Me is not { } me) return Unauthorized();
        var (error, message) = await _stays.CancelAsync(me, id);
        return error != null ? BadRequest(new { message = error }) : Ok(new { success = true, message = await PushAsync(message) });
    }
}

public class StayRequest
{
    public string? PartnerId { get; set; }
    public string? Kind { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Note { get; set; }
}
