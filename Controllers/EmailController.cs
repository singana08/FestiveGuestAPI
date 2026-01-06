using Microsoft.AspNetCore.Mvc;
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
}