using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.Services;
using FestiveGuestAPI.DTOs;
using FestiveGuestAPI.Models;
using Azure.Data.Tables;
using System.Security.Claims;
using System.Text.Json;

namespace FestiveGuestAPI.Controllers;

public class ConfirmUploadDto
{
    public string ImageUrl { get; set; } = string.Empty;
}

public class PublicProfileRequest
{
    public string UserId { get; set; } = string.Empty;
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IFileUploadService _fileUploadService;
    private readonly TableServiceClient _tableServiceClient;

    public UserController(IUserRepository userRepository, IFileUploadService fileUploadService, TableServiceClient tableServiceClient)
    {
        _userRepository = userRepository;
        _fileUploadService = fileUploadService;
        _tableServiceClient = tableServiceClient;
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserRequest request)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        request.UserId = userId;
        var authService = HttpContext.RequestServices.GetService<IAuthService>();
        var result = await authService!.UpdateUserAsync(request);

        if (!result.Success)
            return BadRequest(new { error = result.Message });

        return Ok(result);
    }

    /// <summary>Verify the signed-in member's phone after sign-up (OTP done via /api/phone-otp with purpose "verify").</summary>
    [HttpPost("verify-phone")]
    public async Task<IActionResult> VerifyPhone([FromBody] VerifyPhoneRequest request)
    {
        var userId = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null) return NotFound(new { message = "User not found" });

        var phone = (request.Phone ?? "").Trim();
        if (phone.Length < 10 || !phone.All(char.IsDigit))
            return BadRequest(new { message = "Enter a valid mobile number" });

        var sms = HttpContext.RequestServices.GetRequiredService<ISmsOtpService>();
        if (sms.ValidateVerificationToken(request.VerificationToken, phone, "verify") == null)
            return BadRequest(new { message = "That code has expired. Please request a new one." });

        var other = await _userRepository.GetVerifiedUserByPhoneAsync(phone);
        if (other != null && other.RowKey != user.RowKey)
            return Conflict(new { message = "This number is already verified on another account." });

        user.Phone = phone;
        user.IsVerified = true;
        await _userRepository.UpdateUserAsync(user);
        return Ok(new { success = true, isVerified = true });
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null)
        {
            return NotFound("User not found");
        }

        // Generate SAS URL for profile image if exists
        string profileImageUrl = user.ProfileImageUrl;
        if (!string.IsNullOrEmpty(profileImageUrl))
        {
            try
            {
                var uri = new Uri(profileImageUrl);
                var fileName = Path.GetFileName(uri.LocalPath);
                profileImageUrl = _fileUploadService.GenerateReadSasUrl(fileName, "profile-images");
            }
            catch
            {
                // If parsing fails, keep original URL
            }
        }

        // Parse hosting areas if available
        List<HostingAreaDto>? hostingAreas = null;
        if (!string.IsNullOrEmpty(user.HostingAreas))
        {
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                hostingAreas = System.Text.Json.JsonSerializer.Deserialize<List<HostingAreaDto>>(user.HostingAreas, options);
                hostingAreas = hostingAreas?.Where(h => !string.IsNullOrEmpty(h.State) || h.Cities.Any()).ToList();
                if (hostingAreas?.Count == 0) hostingAreas = null;
            }
            catch { }
        }

        // Calculate successful referrals
        int successfulReferrals = 0;
        if (!string.IsNullOrEmpty(user.ReferralCode))
        {
            var allUsers = await _userRepository.GetAllUsersAsync();
            successfulReferrals = allUsers.Count(u => 
                !string.IsNullOrEmpty(u.ReferredBy) && 
                u.ReferredBy.Equals(user.ReferralCode, StringComparison.OrdinalIgnoreCase) &&
                u.Status == "Active"
            );
        }

        // Get subscription status
        var subscriptionRepository = HttpContext.RequestServices.GetService<ISubscriptionRepository>();
        var subscription = await subscriptionRepository!.GetSubscriptionByUserIdAsync(user.RowKey);

        var userDto = new UserDto
        {
            UserId = user.RowKey,
            Name = user.Name,
            Email = MaskEmail(user.Email),
            Phone = PhoneMask.Apply(user.Phone),
            UserType = user.UserType,
            Status = user.Status,
            Location = user.Location,
            Bio = user.Bio,
            ProfileImageUrl = profileImageUrl,
            IsVerified = user.IsVerified,
            AdminVerified = user.AdminVerified,
            CreatedDate = user.CreatedDate,
            ReferralCode = user.ReferralCode,
            HostingAreas = hostingAreas,
            SuccessfulReferrals = successfulReferrals,
            ReferralPoints = user.ReferralPoints,
            SubscriptionStatus = subscription?.SubscriptionStatus ?? "free",
            NotificationPreferences = NotificationPreferencesDto.Parse(user.NotificationPreferences)
        };

        return Ok(userDto);
    }

    [HttpPost("public-profile")]
    public async Task<IActionResult> GetPublicProfile([FromBody] PublicProfileRequest request)
    {
        var user = await _userRepository.GetUserByIdAsync(request.UserId);
        return user == null ? NotFound("User not found") : Ok(await PublicProfileOf(user));
    }

    /// <summary>Profile links carry the member's name; older links swapped spaces for hyphens, so try both (exact first, for names like Mary-Ann).</summary>
    private async Task<UserEntity?> FindByProfileName(string userName)
    {
        var name = Uri.UnescapeDataString(userName);
        return await _userRepository.GetUserByNameAsync(name)
            ?? (name.Contains('-') ? await _userRepository.GetUserByNameAsync(name.Replace("-", " ")) : null);
    }

    [HttpGet("public-profile-by-name/{userName}")]
    public async Task<IActionResult> GetPublicProfileByName(string userName)
    {
        var user = await FindByProfileName(userName);
        return user == null ? NotFound("User not found") : Ok(await PublicProfileOf(user));
    }

    /// <summary>What other members see on a profile — never the email or phone number.</summary>
    private async Task<object> PublicProfileOf(UserEntity user)
    {
        var services = HttpContext.RequestServices;

        string profileImageUrl = user.ProfileImageUrl;
        if (!string.IsNullOrEmpty(profileImageUrl))
        {
            try { profileImageUrl = _fileUploadService.GenerateReadSasUrl(Path.GetFileName(new Uri(profileImageUrl).LocalPath), "profile-images"); }
            catch { }
        }

        List<HostingAreaDto>? hostingAreas = null;
        if (!string.IsNullOrEmpty(user.HostingAreas))
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                hostingAreas = JsonSerializer.Deserialize<List<HostingAreaDto>>(user.HostingAreas, options)?
                    .Where(h => !string.IsNullOrEmpty(h.State) || h.Cities.Any()).ToList();
                if (hostingAreas?.Count == 0) hostingAreas = null;
            }
            catch { }
        }

        var reviews = await services.GetRequiredService<IReviewService>().VisibleAboutAsync(user.RowKey);
        var badges = await services.GetRequiredService<IMemberStats>().ResponseBadgesAsync();

        // "Their posts": what the member has up right now — a host's listings, a traveller's trip requests.
        IEnumerable<object> posts = user.UserType switch
        {
            "Host" => (await services.GetRequiredService<IHostPostRepository>().GetByUserIdAsync(user.RowKey))
                .Where(p => p.Status == "Active").OrderByDescending(p => p.CreatedAt).Take(10),
            "Guest" => (await services.GetRequiredService<IGuestPostRepository>().GetByUserIdAsync(user.RowKey))
                .Where(p => p.Status == "Active").OrderByDescending(p => p.CreatedAt).Take(10).Select(GuestPostsController.ToResponse),
            _ => Array.Empty<object>(),
        };

        return new
        {
            id = user.RowKey,
            userId = user.RowKey,
            name = user.Name,
            isVerified = user.IsVerified, // email and phone deliberately not shared
            adminVerified = user.AdminVerified, // "Verified by FestiveGuest" — how and since when, never the note
            verifiedHow = user.AdminVerified ? user.AdminVerifiedHow : null,
            verifiedSince = user.AdminVerified ? user.AdminVerifiedAt : null,
            userType = user.UserType,
            location = user.Location,
            bio = user.Bio,
            profileImageUrl,
            hostingAreas,
            createdAt = user.CreatedDate,
            status = user.Status,
            averageRating = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : 0,
            totalReviews = reviews.Count,
            reviews, // published only (see ReviewService)
            gallery = user.UserType == "Host" ? services.GetRequiredService<IGalleryService>().View(user) : new List<GalleryPhotoView>(),
            responseBadge = badges.GetValueOrDefault(user.RowKey),
            confirmedStays = (await services.GetRequiredService<IStayService>().CompletedCountsAsync()).GetValueOrDefault(user.RowKey),
            posts = posts.ToList(),
        };
    }

    [HttpGet("reviews/{userId}")]
    public async Task<IActionResult> GetUserReviews(string userId) =>
        Ok(await HttpContext.RequestServices.GetRequiredService<IReviewService>().VisibleAboutAsync(userId));

    [HttpGet("reviews-by-name/{userName}")]
    public async Task<IActionResult> GetUserReviewsByName(string userName)
    {
        var user = await FindByProfileName(userName);
        return Ok(user == null
            ? new List<ReviewView>()
            : await HttpContext.RequestServices.GetRequiredService<IReviewService>().VisibleAboutAsync(user.RowKey));
    }

    [HttpGet("upload-sas-token")]
    public async Task<IActionResult> GetUploadSasToken()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var sasUrl = await _fileUploadService.GenerateUploadSasTokenAsync(userId);
            return Ok(new { sasUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("confirm-upload")]
    public async Task<IActionResult> ConfirmUpload([FromBody] ConfirmUploadDto dto)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        // Only a photo this member uploaded — otherwise anyone could wear another member's face.
        if (!_fileUploadService.IsOwnProfileImage(dto.ImageUrl ?? "", userId))
            return BadRequest(new { error = "That isn't a photo you uploaded." });

        try
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user != null)
            {
                user.ProfileImageUrl = new Uri(dto.ImageUrl!).GetLeftPart(UriPartial.Path); // never store a SAS query
                await _userRepository.UpdateUserAsync(user);
            }
            return Ok(new { message = "Profile updated" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("upload-profile-image")]
    public async Task<IActionResult> UploadProfileImage(IFormFile file)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized(new { error = "User not authenticated" });
        if (file == null) return BadRequest(new { error = "No file provided" });

        try
        {
            var imageUrl = await _fileUploadService.UploadProfileImageAsync(file, userId);
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user != null)
            {
                user.ProfileImageUrl = imageUrl;
                await _userRepository.UpdateUserAsync(user);
            }
            return Ok(new { imageUrl, message = "Profile image uploaded successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return email;
        
        var parts = email.Split('@');
        var username = parts[0];
        var domain = parts[1];
        
        if (username.Length <= 2)
            return $"{username[0]}***@{domain}";
        
        return $"{username[0]}{new string('*', username.Length - 2)}{username[^1]}@{domain}";
    }
}

public class VerifyPhoneRequest
{
    public string? Phone { get; set; }
    public string? VerificationToken { get; set; }
}
