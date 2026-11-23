using System.Security.Claims;
using FestiveGuestAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FestiveGuestAPI.Controllers;

/// <summary>Guest ↔ host reviews. The rules (who can review, when reviews appear) live in ReviewService.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _reviews;

    public ReviewController(IReviewService reviews) => _reviews = reviews;

    private string? Me => User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>Can I review this member, and what did I write so far?</summary>
    [HttpGet("status/{userId}")]
    public async Task<IActionResult> Status(string userId) =>
        Me is { } me ? Ok(await _reviews.StatusAsync(me, userId)) : Unauthorized();

    /// <summary>Write or edit (while it's still hidden) my review of a member.</summary>
    [HttpPost]
    public async Task<IActionResult> SubmitReview([FromBody] ReviewRequest request)
    {
        if (Me is not { } me) return Unauthorized();
        var error = await _reviews.SubmitAsync(me, request.UserId ?? "", request.Rating, request.Comment);
        return error == null
            ? Ok(new { success = true, message = "Thanks! Your review appears once they've reviewed you too, or after 14 days." })
            : BadRequest(new { success = false, message = error });
    }

    /// <summary>One public reply to a review about me.</summary>
    [HttpPost("{reviewId}/reply")]
    public async Task<IActionResult> Reply(string reviewId, [FromBody] ReplyRequest request)
    {
        if (Me is not { } me) return Unauthorized();
        var error = await _reviews.ReplyAsync(me, reviewId, request.Text);
        return error == null ? Ok(new { success = true }) : BadRequest(new { success = false, message = error });
    }
}

public class ReviewRequest
{
    public string? UserId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class ReplyRequest
{
    public string? Text { get; set; }
}
