using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.Services;
using Azure.Data.Tables;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReferralController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly TableServiceClient _tableServiceClient;

    public ReferralController(IUserRepository userRepository, TableServiceClient tableServiceClient)
    {
        _userRepository = userRepository;
        _tableServiceClient = tableServiceClient;
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateReferralCode([FromBody] ValidateReferralRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReferralCode))
            return BadRequest(new { valid = false, message = "Referral code is required" });

        try
        {
            var usersTable = _tableServiceClient.GetTableClient("Users");
            var query = usersTable.QueryAsync<UserEntity>(filter: $"ReferralCode eq '{request.ReferralCode}'");
            
            await foreach (var user in query)
            {
                return Ok(new { 
                    valid = true, 
                    message = "Valid referral code",
                    referrerName = user.Name
                });
            }

            return Ok(new { valid = false, message = "Invalid referral code" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { valid = false, message = ex.Message });
        }
    }
}

public class ValidateReferralRequest
{
    public string ReferralCode { get; set; } = string.Empty;
}
