using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.Models;
using FestiveGuestAPI.Services;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedbackController : ControllerBase
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly IEmailService _email;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(TableServiceClient tableServiceClient, IEmailService email, ILogger<FeedbackController> logger)
    {
        _tableServiceClient = tableServiceClient;
        _email = email;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Invalid request data", errors = ModelState });

        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var feedbackTable = _tableServiceClient.GetTableClient("Feedback");
            await feedbackTable.CreateIfNotExistsAsync();

            var feedbackEntity = new FeedbackEntity
            {
                PartitionKey = "FEEDBACK",
                RowKey = Guid.NewGuid().ToString(),
                UserId = userId,
                Name = request.Name,
                Email = request.Email,
                UserType = request.UserType,
                Message = request.Message,
                CreatedDate = DateTime.UtcNow
            };

            await feedbackTable.AddEntityAsync(feedbackEntity);

            // Saved first, so a mail hiccup never loses feedback; the email is how someone actually sees it.
            try { await _email.SendFeedbackAsync(feedbackEntity); }
            catch (Exception ex) { _logger.LogWarning(ex, "Feedback {Id} saved but the support email failed", feedbackEntity.RowKey); }

            return Ok(new { success = true, message = "Feedback submitted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save feedback");
            return StatusCode(500, new { success = false, message = "We couldn't send your feedback. Please try again later." });
        }
    }
}

public class FeedbackRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string UserType { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;
}
