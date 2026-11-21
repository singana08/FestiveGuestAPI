using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FestiveGuestAPI.Services;
using FestiveGuestAPI.Models;
using FestiveGuestAPI.DTOs;
using System.Security.Claims;

namespace FestiveGuestAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GuestPostsController : ControllerBase
    {
        private readonly IGuestPostRepository _guestPostRepository;
        private readonly INotificationService _notificationService;

        public GuestPostsController(IGuestPostRepository guestPostRepository, INotificationService notificationService)
        {
            _guestPostRepository = guestPostRepository;
            _notificationService = notificationService;
        }

        // ── Guide requests (a local for a few hours) ──
        private static readonly HashSet<string> NeedTypes = new() { "stay", "guide", "both" };
        private static readonly HashSet<string> HelpTypes = new()
        {
            "hospital_visit", "govt_office", "exam_centre", "interview", "court", "university_admission",
            "bank_paperwork", "translation", "form_filling", "city_orientation", "elderly_companion",
        };

        private static List<string> Clean(List<string>? items) =>
            items?.Select(x => x?.Trim() ?? "").Where(x => x.Length > 0).Distinct().ToList() ?? new List<string>();

        private static List<string> SplitCsv(string? csv) =>
            string.IsNullOrEmpty(csv) ? new List<string>() : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        /// <summary>Returns an error message, or null when the guide details suit the need type.</summary>
        private static string? ValidateGuide(string needType, List<string> helpWith, int? hours, List<string> languages, int? budgetPerHour)
        {
            if (!NeedTypes.Contains(needType)) return "needType must be 'stay', 'guide' or 'both'";
            if (needType == "stay") return null;
            if (helpWith.Count == 0) return "Pick at least one thing you need help with";
            if (helpWith.Any(h => !HelpTypes.Contains(h))) return "Unknown help type";
            if (hours is null or < 1 or > 12) return "Guide hours must be between 1 and 12";
            if (languages.Count > 10 || languages.Any(l => l.Length > 30 || l.Contains(','))) return "Invalid languages";
            if (budgetPerHour is < 0 or > 100000) return "Budget per hour must be between 0 and 100000";
            return null;
        }

        private static void ApplyGuide(GuestPostEntity post, string needType, List<string> helpWith, int? hours, List<string> languages, int? budgetPerHour)
        {
            var guide = needType != "stay";
            post.NeedType = needType;
            post.GuideHelpWith = guide ? string.Join(",", helpWith) : "";
            post.GuideHours = guide ? hours : null;
            post.GuideLanguages = guide ? string.Join(",", languages) : "";
            post.GuideBudgetPerHour = guide ? budgetPerHour : null;
        }

        // Shared response shape. The poster's email is deliberately left out —
        // posts are visible to other members, and no client uses it.
        private static object ToResponse(GuestPostEntity p) => new
        {
            rowKey = p.RowKey,
            userId = p.UserId,
            userName = p.UserName,
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
            updatedAt = p.UpdatedAt,
            needType = string.IsNullOrEmpty(p.NeedType) ? "stay" : p.NeedType,
            guideHelpWith = p.GuideHelpWith,
            guideHours = p.GuideHours,
            guideLanguages = p.GuideLanguages,
            guideBudgetPerHour = p.GuideBudgetPerHour
        };

        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] CreateGuestPostRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userName = User.FindFirst("name")?.Value;
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

                var needType = (request.NeedType ?? "stay").Trim().ToLowerInvariant();
                var helpWith = Clean(request.GuideHelpWith);
                var languages = Clean(request.GuideLanguages);
                var guideError = ValidateGuide(needType, helpWith, request.GuideHours, languages, request.GuideBudgetPerHour);
                if (guideError != null)
                {
                    return BadRequest(new { message = guideError });
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
                ApplyGuide(post, needType, helpWith, request.GuideHours, languages, request.GuideBudgetPerHour);

                var createdPost = await _guestPostRepository.CreateAsync(post);

                // Fire-and-forget: notify matching hosts
                _ = Task.Run(() => _notificationService.NotifyHostsAboutGuestPostAsync(createdPost));

                return CreatedAtAction(nameof(GetPost), new { id = createdPost.RowKey }, createdPost);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPosts()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }
                
                var posts = await _guestPostRepository.GetAllActiveAsync();
                var userRepository = HttpContext.RequestServices.GetService<IUserRepository>();
                var currentUser = await userRepository?.GetUserByIdAsync(userId);
                
                if (currentUser?.UserType == "Host")
                {
                    if (string.IsNullOrEmpty(currentUser.HostingAreas))
                    {
                        return Ok(new List<object>());
                    }
                    
                    try
                    {
                        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var hostingAreas = System.Text.Json.JsonSerializer.Deserialize<List<HostingAreaDto>>(currentUser.HostingAreas, options);
                        
                        if (hostingAreas?.Any() != true)
                        {
                            return Ok(new List<object>());
                        }
                        
                        var hostCities = hostingAreas
                            .SelectMany(h => h.Cities.Select(c => c.ToLower()))
                            .ToHashSet();
                        
                        posts = posts.Where(p => 
                            hostCities.Any(city => p.Location.ToLower().Contains(city))
                        ).ToList();
                    }
                    catch { return Ok(new List<object>()); }
                }
                
                return Ok(posts.Select(ToResponse));
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
                return Ok(ToResponse(post));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        [HttpGet("my-posts")]
        public async Task<IActionResult> GetMyPosts()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var allPosts = await _guestPostRepository.GetAllActiveAsync();
                var myPosts = allPosts.Where(p => p.UserId == userId).ToList();

                return Ok(myPosts.Select(ToResponse));
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

                // Clients that don't know about guide requests (no needType sent) keep what's stored.
                var guideAware = request.NeedType != null;
                var needType = guideAware ? request.NeedType!.Trim().ToLowerInvariant() : (string.IsNullOrEmpty(post.NeedType) ? "stay" : post.NeedType);
                var helpWith = guideAware ? Clean(request.GuideHelpWith) : SplitCsv(post.GuideHelpWith);
                var languages = guideAware ? Clean(request.GuideLanguages) : SplitCsv(post.GuideLanguages);
                var hours = guideAware ? request.GuideHours : post.GuideHours;
                var budget = guideAware ? request.GuideBudgetPerHour : post.GuideBudgetPerHour;
                var guideError = ValidateGuide(needType, helpWith, hours, languages, budget);
                if (guideError != null)
                {
                    return BadRequest(new { message = guideError });
                }
                ApplyGuide(post, needType, helpWith, hours, languages, budget);
                post.UpdatedAt = DateTime.UtcNow;

                var updated = await _guestPostRepository.UpdateAsync(post);

                // Fire-and-forget: notify matching hosts about update
                _ = Task.Run(() => _notificationService.NotifyHostsAboutGuestPostAsync(updated, isUpdate: true));

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
        public string? State { get; set; }
        public string? City { get; set; }
        public string? UserName { get; set; }
        public DateTime? PlanningDate { get; set; }
        public List<string>? Facilities { get; set; }
        public int? Visitors { get; set; }
        public int? Days { get; set; }
        public DateTime? VisitingDate { get; set; }
        public string? NeedType { get; set; }
        public List<string>? GuideHelpWith { get; set; }
        public int? GuideHours { get; set; }
        public List<string>? GuideLanguages { get; set; }
        public int? GuideBudgetPerHour { get; set; }
    }

    public class UpdateGuestPostRequest
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Location { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }
        public string? UserName { get; set; }
        public DateTime? PlanningDate { get; set; }
        public List<string>? Facilities { get; set; }
        public int? Visitors { get; set; }
        public int? Days { get; set; }
        public DateTime? VisitingDate { get; set; }
        public string? NeedType { get; set; }
        public List<string>? GuideHelpWith { get; set; }
        public int? GuideHours { get; set; }
        public List<string>? GuideLanguages { get; set; }
        public int? GuideBudgetPerHour { get; set; }
    }
}