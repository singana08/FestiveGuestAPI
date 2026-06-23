using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.DTOs;
using FestiveGuestAPI.Services;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly IEmailService _emailService;

    public EmailController(IEmailService emailService)
    {
        _emailService = emailService;
    }

    [HttpPost("send-otp")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> SendOTP([FromBody] SendOTPRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _emailService.SendOTPAsync(request);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("validate-otp")]
    public async Task<IActionResult> ValidateOTP([FromBody] ValidateOTPRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _emailService.ValidateOTPAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("send-invitations")]
    [Authorize]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> SendInvitations([FromBody] SendInvitationsRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.Emails.Count == 0)
            return BadRequest(new EmailResponse { Success = false, Message = "No emails provided." });

        if (request.Emails.Count > 20)
            return BadRequest(new EmailResponse { Success = false, Message = "Maximum 20 invitations at a time." });

        var result = await _emailService.SendInvitationsAsync(request);
        return Ok(result);
    }
}