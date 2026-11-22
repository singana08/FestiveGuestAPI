using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FestiveGuestAPI.DTOs;
using FestiveGuestAPI.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notifications;

    public AuthController(IAuthService authService, IUserRepository userRepository, INotificationService notifications)
    {
        _authService = authService;
        _userRepository = userRepository;
        _notifications = notifications;
    }

    private string UserAgent => Request.Headers.UserAgent.ToString();

    // Emails go out in the background so signing in isn't slowed down by the mail server.
    private void AlertSignIn(AuthResponse result, string method)
    {
        if (result.User == null) return;
        var (user, userAgent) = (result.User, UserAgent);
        _ = Task.Run(() => _notifications.NotifySignInAsync(user, userAgent, method));
    }

    private void AlertPasswordChanged(Models.UserEntity? user)
    {
        var userAgent = UserAgent;
        _ = Task.Run(() => _notifications.NotifyPasswordChangedAsync(user, userAgent));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.RegisterAsync(request);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.LoginAsync(request);

        if (!result.Success)
        {
            return Unauthorized(result);
        }

        AlertSignIn(result, "email and password");
        return Ok(result);
    }

    [HttpPut("update")]
    [Authorize]
    public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tokenUserId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(tokenUserId) || tokenUserId != request.UserId)
        {
            return Forbid();
        }

        var result = await _authService.UpdateUserAsync(request);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("google-login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.GoogleLoginAsync(request.IdToken);

        if (!result.Success)
            return BadRequest(result);

        AlertSignIn(result, "Google");
        return Ok(result);
    }

    [HttpPost("google-verify")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> GoogleVerify([FromBody] GoogleLoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.GoogleVerifyAsync(request.IdToken);

        if (!result.Success)
            return result.AlreadyRegistered ? Conflict(result) : BadRequest(result);

        return Ok(result);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.ResetPasswordAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        AlertPasswordChanged(await _userRepository.GetUserByEmailAsync(request.Email.ToLower()));
        return Ok(result);
    }

    [HttpPost("reset-password-phone")]
    public async Task<IActionResult> ResetPasswordWithPhone([FromBody] ResetPasswordPhoneRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.ResetPasswordWithPhoneAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        AlertPasswordChanged(await _userRepository.GetVerifiedUserByPhoneAsync(request.Phone));
        return Ok(result);
    }

    [HttpPost("login-with-phone")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> LoginWithPhone([FromBody] PhoneLoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.LoginWithPhoneAsync(request);

        if (!result.Success)
        {
            return Unauthorized(result);
        }

        AlertSignIn(result, "a phone code");
        return Ok(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { Success = false, Message = "Invalid token." });
        }

        var result = await _authService.ChangePasswordAsync(request, userId);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        AlertPasswordChanged(await _userRepository.GetUserByIdAsync(userId));
        return Ok(result);
    }

}