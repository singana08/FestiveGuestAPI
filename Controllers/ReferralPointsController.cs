using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.DTOs;
using FestiveGuestAPI.Services;
using FestiveGuestAPI.Configuration;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReferralPointsController : ControllerBase
{
    private readonly IReferralPointsService _pointsService;
    private readonly IUserRepository _userRepository;

    public ReferralPointsController(IReferralPointsService pointsService, IUserRepository userRepository)
    {
        _pointsService = pointsService;
        _userRepository = userRepository;
    }

    [HttpPost("redeem")]
    public async Task<IActionResult> RedeemPoints()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        if (user.ReferralPoints < 500)
            return BadRequest(new { success = false, message = "Insufficient points. Need 500 points to redeem." });

        var success = await _pointsService.RedeemPointsForSubscriptionAsync(userId);
        
        if (!success)
            return BadRequest(new { success = false, message = "Cannot redeem. You already have an active paid subscription." });

        var updatedUser = await _userRepository.GetUserByIdAsync(userId);
        return Ok(new RedeemPointsResponse
        {
            Success = true,
            Message = "Points redeemed successfully! 3 months subscription activated.",
            RemainingPoints = updatedUser!.ReferralPoints,
            SubscriptionStatus = "paid",
            SubscriptionExpiryDate = DateTime.UtcNow.AddMonths(3)
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetPointsHistory()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var history = await _pointsService.GetPointsHistoryAsync(userId);
        var user = await _userRepository.GetUserByIdAsync(userId);
        
        var formattedHistory = history.Select(h => new
        {
            userName = user?.Name ?? "Unknown",
            points = h.Points,
            type = h.Type,
            description = h.Description,
            createdDate = h.CreatedDate
        });
        
        return Ok(formattedHistory);
    }

    [HttpPost("adjust")]
    [AdminAuthorize]
    public async Task<IActionResult> AdjustPoints([FromBody] AdjustPointsRequest request)
    {
        var adminEmail = User.FindFirst("email")?.Value ?? "unknown";
        
        await _pointsService.AdjustPointsAsync(request.UserId, request.Points, request.Description, adminEmail);
        
        var user = await _userRepository.GetUserByIdAsync(request.UserId);
        return Ok(new 
        { 
            success = true, 
            message = "Points adjusted successfully",
            newBalance = user?.ReferralPoints ?? 0
        });
    }
}
