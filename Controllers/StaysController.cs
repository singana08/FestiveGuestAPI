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

    /// <summary>
    /// "My trips" (travellers) / "My guests" (hosts): open and confirmed stays with the other person's
    /// name, city and photo, the host's gallery cover, nights, and whether I've reviewed them yet.
    /// </summary>
    [HttpGet("trips")]
    public async Task<IActionResult> Trips([FromServices] IUserRepository users, [FromServices] IGalleryService gallery,
        [FromServices] IFileUploadService files, [FromServices] IReviewService reviews)
    {
        if (Me is not { } me) return Unauthorized();
        var reviewed = await reviews.ReviewedByAsync(me);
        var people = new Dictionary<string, UserEntity?>();
        var trips = new List<object>();
        foreach (var s in (await _stays.MineAsync(me)).Where(s => s.Status is StayService.Pending or StayService.Confirmed))
        {
            var otherId = s.RequesterId == me ? s.PartnerId : s.RequesterId;
            if (!people.TryGetValue(otherId, out var other)) people[otherId] = other = await users.GetUserByIdAsync(otherId);
            trips.Add(new
            {
                stay = s,
                other = new
                {
                    id = otherId,
                    name = other?.Name ?? (s.RequesterId == me ? s.PartnerName : s.RequesterName),
                    userType = other?.UserType ?? "",
                    location = other?.Location ?? "",
                    photoUrl = MemberPhotos.Profile(other, files),
                    coverUrl = MemberPhotos.Cover(other, gallery),
                },
                nights = Math.Max(0, (s.EndDate.Date - s.StartDate.Date).Days),
                reviewed = reviewed.Contains(otherId),
            });
        }
        return Ok(trips);
    }

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
