using Microsoft.AspNetCore.Mvc;
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

    public AuthController(IAuthService authService, IUserRepository userRepository)
    {
        _authService = authService;
        _userRepository = userRepository;
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

        return Ok(result);
    }

    [HttpPut("update")]
    public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.UpdateUserAsync(request);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

}