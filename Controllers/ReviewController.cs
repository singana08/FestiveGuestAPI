using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.Models;
using FestiveGuestAPI.Services;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReviewController : ControllerBase
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly IUserRepository _userRepository;

    public ReviewController(TableServiceClient tableServiceClient, IUserRepository userRepository)
    {
        _tableServiceClient = tableServiceClient;
        _userRepository = userRepository;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitReview([FromBody] ReviewRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var reviewerId = User.FindFirst("userId")?.Value;
        
        if (string.IsNullOrEmpty(reviewerId))
            return Unauthorized();

        try
        {
            var reviewTable = _tableServiceClient.GetTableClient("Reviews");
            await reviewTable.CreateIfNotExistsAsync();

            // Check if user already reviewed this person
            var existingReview = reviewTable.QueryAsync<ReviewEntity>(r => r.PartitionKey == request.UserId && r.ReviewerId == reviewerId);
            await foreach (var review in existingReview)
            {
                return BadRequest(new { success = false, message = "You have already reviewed this user" });
            }

            // Get reviewer name from Users table
            var reviewer = await _userRepository.GetUserByIdAsync(reviewerId);
            var reviewerName = reviewer?.Name ?? "User";

            var reviewEntity = new ReviewEntity
            {
                PartitionKey = request.UserId,
                RowKey = Guid.NewGuid().ToString(),
                ReviewerId = reviewerId,
                ReviewerName = reviewerName,
                Rating = request.Rating,
                Comment = request.Comment,
                CreatedAt = DateTime.UtcNow
            };

            await reviewTable.AddEntityAsync(reviewEntity);

            return Ok(new { success = true, message = "Review submitted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Failed to submit review: {ex.Message}" });
        }
    }
}

public class ReviewRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [Required]
    [MaxLength(500)]
    public string Comment { get; set; } = string.Empty;
}
