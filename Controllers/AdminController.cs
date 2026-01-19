using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.Services;
using FestiveGuestAPI.Configuration;
using Azure.Data.Tables;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[AdminAuthorize]
public class AdminController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly TableServiceClient _tableServiceClient;

    public AdminController(
        IUserRepository userRepository,
        ISubscriptionRepository subscriptionRepository,
        TableServiceClient tableServiceClient)
    {
        _userRepository = userRepository;
        _subscriptionRepository = subscriptionRepository;
        _tableServiceClient = tableServiceClient;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            var users = await _userRepository.GetAllUsersAsync();
            var usersWithSubscriptions = new List<object>();

            foreach (var user in users)
            {
                var subscription = await _subscriptionRepository.GetSubscriptionByUserIdAsync(user.RowKey);
                
                // Count successful referrals
                int successfulReferrals = 0;
                if (!string.IsNullOrEmpty(user.ReferralCode))
                {
                    successfulReferrals = users.Count(u => 
                        !string.IsNullOrEmpty(u.ReferredBy) && 
                        u.ReferredBy.Equals(user.ReferralCode, StringComparison.OrdinalIgnoreCase) &&
                        u.Status == "Active"
                    );
                }

                usersWithSubscriptions.Add(new
                {
                    userId = user.RowKey,
                    name = user.Name,
                    email = user.Email,
                    phone = user.Phone,
                    userType = user.UserType,
                    status = user.Status,
                    createdDate = user.CreatedDate,
                    referralCode = user.ReferralCode,
                    referredBy = user.ReferredBy,
                    successfulReferrals = successfulReferrals,
                    subscriptionStatus = subscription?.SubscriptionStatus ?? "free",
                    paymentVerifiedTimestamp = subscription?.PaymentVerifiedTimestamp,
                    lastUpdated = subscription?.Timestamp?.DateTime
                });
            }

            return Ok(usersWithSubscriptions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("listpayments")]
    public async Task<IActionResult> GetAllPayments()
    {
        try
        {
            var tableClient = _tableServiceClient.GetTableClient("Payments");
            var payments = tableClient.QueryAsync<PaymentEntity>();
            
            var paymentList = new List<object>();
            await foreach (var payment in payments)
            {
                paymentList.Add(new
                {
                    id = payment.RowKey,
                    userId = payment.UserId,
                    upiReference = payment.UpiReference,
                    amount = payment.Amount,
                    status = payment.Status,
                    submittedDate = payment.SubmittedDate,
                    verifiedDate = payment.VerifiedDate,
                    adminNotes = payment.AdminNotes
                });
            }

            return Ok(paymentList);
        }
        catch
        {
            return Ok(new List<object>());
        }
    }
}
