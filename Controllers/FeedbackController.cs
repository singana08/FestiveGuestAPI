using Microsoft.AspNetCore.Mvc;
using FestiveGuestAPI.Models;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedbackController : ControllerBase
{
    private readonly TableServiceClient _tableServiceClient;

    public FeedbackController(TableServiceClient tableServiceClient)
    {
        _tableServiceClient = tableServiceClient;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Invalid request data", errors = ModelState });

        try
        {
            var feedbackTable = _tableServiceClient.GetTableClient("Feedback");
            await feedbackTable.CreateIfNotExistsAsync();

            var feedbackEntity = new FeedbackEntity
            {
                PartitionKey = "FEEDBACK",
                RowKey = Guid.NewGuid().ToString(),
                Name = request.Name,
                Email = request.Email,
                UserType = request.UserType,
                Message = request.Message,
                CreatedDate = DateTime.UtcNow
            };

            await feedbackTable.AddEntityAsync(feedbackEntity);

            return Ok(new { success = true, message = "Feedback submitted successfully" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Feedback error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return StatusCode(500, new { success = false, message = $"Failed to submit feedback: {ex.Message}" });
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
