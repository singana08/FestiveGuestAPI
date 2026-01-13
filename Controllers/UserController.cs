using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.Services;
using FestiveGuestAPI.DTOs;
using FestiveGuestAPI.Models;
using Azure.Data.Tables;
using System.Security.Claims;

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
                hostingAreas = System.Text.Json.JsonSerializer.Deserialize<List<HostingAreaDto>>(user.HostingAreas);
            }
            catch { }
        }

        var userDto = new UserDto
        {
            UserId = user.RowKey,
            Name = user.Name,
            Email = MaskEmail(user.Email),
            Phone = MaskPhone(user.Phone),
            UserType = user.UserType,
            Status = user.Status,
            Location = user.Location,
            Bio = user.Bio,
            ProfileImageUrl = profileImageUrl,
            IsVerified = user.IsVerified,
            CreatedDate = user.CreatedDate,
            ReferralCode = user.ReferralCode,
            HostingAreas = hostingAreas
        };

        return Ok(userDto);
    }

    [HttpGet("browse")]
    public async Task<IActionResult> BrowseUsers()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var currentUser = await _userRepository.GetUserByIdAsync(userId);
        if (currentUser == null)
        {
            return NotFound("User not found");
        }

        // If user is Guest, return Hosts; if user is Host, return Guests
        var targetUserType = currentUser.UserType == "Guest" ? "Host" : "Guest";
        var users = await _userRepository.GetUsersByTypeAsync(targetUserType);

        var userDtos = users.Select(u => {
            // Generate SAS URL for profile image if exists
            string profileImageUrl = u.ProfileImageUrl;
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

            return new UserDto
            {
                UserId = u.RowKey,
                Name = u.Name,
                Email = MaskEmail(u.Email),
                Phone = MaskPhone(u.Phone),
                UserType = u.UserType,
                Status = u.Status,
                Location = u.Location,
                Bio = u.Bio,
                ProfileImageUrl = profileImageUrl,
                IsVerified = u.IsVerified,
                CreatedDate = u.CreatedDate,
                ReferralCode = u.ReferralCode
            };
        }).ToList();

        return Ok(userDtos);
    }

    [HttpPost("public-profile")]
    public async Task<IActionResult> GetPublicProfile([FromBody] PublicProfileRequest request)
    {
        var user = await _userRepository.GetUserByIdAsync(request.UserId);
        if (user == null)
            return NotFound("User not found");

        string profileImageUrl = user.ProfileImageUrl;
        if (!string.IsNullOrEmpty(profileImageUrl))
        {
            try
            {
                var uri = new Uri(profileImageUrl);
                var fileName = Path.GetFileName(uri.LocalPath);
                profileImageUrl = _fileUploadService.GenerateReadSasUrl(fileName, "profile-images");
            }
            catch { }
        }

        // Get reviews and calculate ratings
        var reviewData = await GetReviewsData(request.UserId);

        return Ok(new
        {
            id = user.RowKey,
            userId = user.RowKey,
            name = user.Name,
            email = user.Email,
            userType = user.UserType,
            location = user.Location,
            bio = user.Bio,
            profileImageUrl,
            createdAt = user.CreatedDate,
            status = user.Status,
            averageRating = reviewData.AverageRating,
            totalReviews = reviewData.TotalReviews,
            reviews = reviewData.Reviews
        });
    }

    [HttpGet("public-profile-by-name/{userName}")]
    public async Task<IActionResult> GetPublicProfileByName(string userName)
    {
        var decodedName = Uri.UnescapeDataString(userName).Replace("-", " ");
        var user = await _userRepository.GetUserByNameAsync(decodedName);
        
        if (user == null)
            return NotFound("User not found");

        string profileImageUrl = user.ProfileImageUrl;
        if (!string.IsNullOrEmpty(profileImageUrl))
        {
            try
            {
                var uri = new Uri(profileImageUrl);
                var fileName = Path.GetFileName(uri.LocalPath);
                profileImageUrl = _fileUploadService.GenerateReadSasUrl(fileName, "profile-images");
            }
            catch { }
        }

        // Get reviews and calculate ratings
        var reviewData = await GetReviewsData(user.RowKey);

        return Ok(new
        {
            id = user.RowKey,
            userId = user.RowKey,
            name = user.Name,
            email = user.Email,
            userType = user.UserType,
            location = user.Location,
            bio = user.Bio,
            profileImageUrl,
            createdAt = user.CreatedDate,
            status = user.Status,
            averageRating = reviewData.AverageRating,
            totalReviews = reviewData.TotalReviews,
            reviews = reviewData.Reviews
        });
    }

    private async Task<(double AverageRating, int TotalReviews, List<object> Reviews)> GetReviewsData(string userId)
    {
        try
        {
            var tableClient = _tableServiceClient.GetTableClient("Reviews");
            var reviews = tableClient.QueryAsync<ReviewEntity>(r => r.PartitionKey == userId);
            
            var reviewList = new List<object>();
            var ratings = new List<int>();

            await foreach (var review in reviews)
            {
                reviewList.Add(new
                {
                    reviewerId = review.ReviewerId,
                    reviewerName = review.ReviewerName,
                    rating = review.Rating,
                    comment = review.Comment,
                    createdAt = review.CreatedAt
                });
                ratings.Add(review.Rating);
            }

            var averageRating = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : 0;
            return (averageRating, ratings.Count, reviewList);
        }
        catch
        {
            return (0, 0, new List<object>());
        }
    }

    [HttpGet("reviews/{userId}")]
    public async Task<IActionResult> GetUserReviews(string userId)
    {
        try
        {
            var tableClient = _tableServiceClient.GetTableClient("Reviews");
            var reviews = tableClient.QueryAsync<ReviewEntity>(r => r.PartitionKey == userId);
            
            var reviewList = new List<object>();
            await foreach (var review in reviews)
            {
                reviewList.Add(new
                {
                    reviewerId = review.ReviewerId,
                    reviewerName = review.ReviewerName,
                    rating = review.Rating,
                    comment = review.Comment,
                    createdAt = review.CreatedAt
                });
            }

            return Ok(reviewList);
        }
        catch
        {
            return Ok(new List<object>());
        }
    }

    [HttpGet("reviews-by-name/{userName}")]
    public async Task<IActionResult> GetUserReviewsByName(string userName)
    {
        try
        {
            var decodedName = Uri.UnescapeDataString(userName).Replace("-", " ");
            var user = await _userRepository.GetUserByNameAsync(decodedName);
            
            if (user == null)
                return Ok(new List<object>());

            var tableClient = _tableServiceClient.GetTableClient("Reviews");
            var reviews = tableClient.QueryAsync<ReviewEntity>(r => r.PartitionKey == user.RowKey);
            
            var reviewList = new List<object>();
            await foreach (var review in reviews)
            {
                reviewList.Add(new
                {
                    reviewerId = review.ReviewerId,
                    reviewerName = review.ReviewerName,
                    rating = review.Rating,
                    comment = review.Comment,
                    createdAt = review.CreatedAt
                });
            }

            return Ok(reviewList);
        }
        catch
        {
            return Ok(new List<object>());
        }
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

        try
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user != null)
            {
                user.ProfileImageUrl = dto.ImageUrl;
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

    private string MaskPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 4)
            return phone;
        
        return $"{phone[..2]}{new string('*', phone.Length - 4)}{phone[^2..]}";
    }
}