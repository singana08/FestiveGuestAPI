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
    public class GuestPostsController : ControllerBase
    {
        private readonly IGuestPostRepository _guestPostRepository;

        public GuestPostsController(IGuestPostRepository guestPostRepository)
        {
            _guestPostRepository = guestPostRepository;
        }

        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] CreateGuestPostRequest request)
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

                var post = new GuestPostEntity
                {
                    PartitionKey = "GuestPost",
                    RowKey = $"post_{DateTime.UtcNow.Ticks}_{Guid.NewGuid().ToString("N")[..8]}",
                    UserId = userId,
                    UserName = !string.IsNullOrWhiteSpace(request.UserName) ? request.UserName : (userName ?? "Unknown"),
                    UserEmail = userEmail ?? "",
                    Title = request.Title.Trim(),
                    Content = request.Content.Trim(),
                    Location = request.Location.Trim(),
                    Facilities = request.Facilities != null ? string.Join(",", request.Facilities) : "",
                    Visitors = request.Visitors,
                    Days = request.Days,
                    VisitingDate = request.PlanningDate ?? request.VisitingDate,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var createdPost = await _guestPostRepository.CreateAsync(post);
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
                var posts = await _guestPostRepository.GetAllActiveAsync();
                var response = posts.Select(p => new
                {
                    rowKey = p.RowKey,
                    userId = p.UserId,
                    userName = p.UserName,
                    userEmail = p.UserEmail,
                    title = p.Title,
                    content = p.Content,
                    location = p.Location,
                    facilities = p.Facilities,
                    visitors = p.Visitors,
                    days = p.Days,
                    planningDate = p.VisitingDate,
                    visitingDate = p.VisitingDate,
                    status = p.Status,
                    createdAt = p.CreatedAt,
                    updatedAt = p.UpdatedAt
                });
                return Ok(response);
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
                var post = await _guestPostRepository.GetByIdAsync(id);
                if (post == null)
                {
                    return NotFound("Post not found");
                }
                var response = new
                {
                    rowKey = post.RowKey,
                    userId = post.UserId,
                    userName = post.UserName,
                    userEmail = post.UserEmail,
                    title = post.Title,
                    content = post.Content,
                    location = post.Location,
                    facilities = post.Facilities,
                    visitors = post.Visitors,
                    days = post.Days,
                    planningDate = post.VisitingDate,
                    visitingDate = post.VisitingDate,
                    status = post.Status,
                    createdAt = post.CreatedAt,
                    updatedAt = post.UpdatedAt
                };
                return Ok(response);
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

                var success = await _guestPostRepository.DeleteAsync(id, userId);
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

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePost(string id, [FromBody] UpdateGuestPostRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var post = await _guestPostRepository.GetByIdAsync(id);
                if (post == null)
                {
                    return NotFound("Post not found");
                }

                if (post.UserId != userId)
                {
                    return Forbid();
                }

                post.Title = request.Title?.Trim() ?? post.Title;
                post.Content = request.Content?.Trim() ?? post.Content;
                post.Location = request.Location?.Trim() ?? post.Location;
                post.UserName = request.UserName ?? post.UserName;
                post.Facilities = request.Facilities != null ? string.Join(",", request.Facilities) : post.Facilities;
                post.Visitors = request.Visitors ?? post.Visitors;
                post.Days = request.Days ?? post.Days;
                post.VisitingDate = request.PlanningDate ?? request.VisitingDate ?? post.VisitingDate;
                post.UpdatedAt = DateTime.UtcNow;

                var updated = await _guestPostRepository.UpdateAsync(post);
                return Ok(updated);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }
    }

    public class CreateGuestPostRequest
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Location { get; set; } = "";
        public string? UserName { get; set; }
        public DateTime? PlanningDate { get; set; }
        public List<string>? Facilities { get; set; }
        public int? Visitors { get; set; }
        public int? Days { get; set; }
        public DateTime? VisitingDate { get; set; }
    }

    public class UpdateGuestPostRequest
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Location { get; set; }
        public string? UserName { get; set; }
        public DateTime? PlanningDate { get; set; }
        public List<string>? Facilities { get; set; }
        public int? Visitors { get; set; }
        public int? Days { get; set; }
        public DateTime? VisitingDate { get; set; }
    }
}