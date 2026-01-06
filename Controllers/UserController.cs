using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.Services;
using FestiveGuestAPI.DTOs;
using System.Security.Claims;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IFileUploadService _fileUploadService;

    public UserController(IUserRepository userRepository, IFileUploadService fileUploadService)
    {
        _userRepository = userRepository;
        _fileUploadService = fileUploadService;
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
            ProfileImageUrl = user.ProfileImageUrl,
            IsVerified = user.IsVerified,
            CreatedDate = user.CreatedDate
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

        var userDtos = users.Select(u => new UserDto
        {
            UserId = u.RowKey,
            Name = u.Name,
            Email = MaskEmail(u.Email),
            Phone = MaskPhone(u.Phone),
            UserType = u.UserType,
            Status = u.Status,
            Location = u.Location,
            Bio = u.Bio,
            ProfileImageUrl = u.ProfileImageUrl,
            IsVerified = u.IsVerified,
            CreatedDate = u.CreatedDate
        }).ToList();

        return Ok(userDtos);
    }

    [HttpPost("upload-profile-image")]
    public async Task<IActionResult> UploadProfileImage(IFormFile file)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var imageUrl = await _fileUploadService.UploadProfileImageAsync(file, userId);
            
            // Update user profile with new image URL
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
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Failed to upload image");
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