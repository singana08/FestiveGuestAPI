using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FestiveGuestAPI.DTOs;
using FestiveGuestAPI.Services;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/phone-otp")]
public class PhoneOtpController : ControllerBase
{
    private static readonly string[] ValidPurposes = { "register", "login", "reset", "verify" };

    private readonly ISmsOtpService _smsOtpService;

    public PhoneOtpController(ISmsOtpService smsOtpService)
    {
        _smsOtpService = smsOtpService;
    }

    [HttpPost("send")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> Send([FromBody] SendPhoneOtpRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _smsOtpService.SendOtpAsync(request.Phone);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyPhoneOtpRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!ValidPurposes.Contains(request.Purpose))
            return BadRequest(new PhoneOtpResponse { Success = false, Message = "Invalid purpose." });

        var result = await _smsOtpService.VerifyOtpAsync(request.SessionId, request.Otp, request.Phone, request.Purpose);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
