using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FestiveGuestAPI.Services;
using FestiveGuestAPI.Models;
using System.Security.Claims;

namespace FestiveGuestAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class HostPostsController : ControllerBase
    {
        private readonly IHostPostRepository _hostPostRepository;

        public HostPostsController(IHostPostRepository hostPostRepository)
        {
            _hostPostRepository = hostPostRepository;
        }

        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] CreateHostPostRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userName = User.FindFirst(ClaimTypes.Name)?.Value;
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                if (string.IsNullOrWhiteSpace(request.Title) || 
                    string.IsNullOrWhiteSpace(request.Content) || 
                    string.IsNullOrWhiteSpace(request.Location))
                {
                    return BadRequest("Title, content, and location are required");
                }

                var post = new HostPostEntity
                {
                    PartitionKey = "HostPost",
                    RowKey = $"post_{DateTime.UtcNow.Ticks}_{Guid.NewGuid().ToString("N")[..8]}",
                    UserId = userId,
                    UserName = userName ?? "Unknown",
                    UserEmail = userEmail ?? "",
                    Title = request.Title.Trim(),
                    Content = request.Content.Trim(),
                    Location = request.Location.Trim(),
                    Amenities = request.Amenities != null ? string.Join(",", request.Amenities) : "",
                    MaxGuests = request.MaxGuests,
                    PricePerNight = request.PricePerNight,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var createdPost = await _hostPostRepository.CreateAsync(post);
                return CreatedAtAction(nameof(GetPost), new { id = createdPost.RowKey }, createdPost);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllPosts()
        {
            try
            {
                var posts = await _hostPostRepository.GetAllActiveAsync();
                return Ok(posts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPost(string id)
        {
            try
            {
                var post = await _hostPostRepository.GetByIdAsync(id);
                if (post == null)
                {
                    return NotFound("Post not found");
                }
                return Ok(post);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePost(string id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var success = await _hostPostRepository.DeleteAsync(id, userId);
                if (!success)
                {
                    return NotFound("Post not found or unauthorized");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }
    }

    public class CreateHostPostRequest
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Location { get; set; } = "";
        public List<string>? Amenities { get; set; }
        public int? MaxGuests { get; set; }
        public decimal? PricePerNight { get; set; }
    }
}