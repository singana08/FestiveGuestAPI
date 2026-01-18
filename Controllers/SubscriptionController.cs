using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.DTOs;
using FestiveGuestAPI.Services;
using FestiveGuestAPI.Models;
using FestiveGuestAPI.Configuration;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        ISubscriptionRepository subscriptionRepository,
        IUserRepository userRepository,
        ILogger<SubscriptionController> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetSubscription(string userId)
    {
        try
        {
            var subscription = await _subscriptionRepository.GetSubscriptionByUserIdAsync(userId);
            
            if (subscription == null)
            {
                return Ok(new SubscriptionResponse
                {
                    Success = true,
                    Message = "No subscription found",
                    Subscription = new SubscriptionDto
                    {
                        UserId = userId,
                        SubscriptionStatus = "free",
                        PaymentVerifiedTimestamp = null,
                        LastUpdated = null
                    }
                });
            }

            return Ok(new SubscriptionResponse
            {
                Success = true,
                Message = "Subscription retrieved successfully",
                Subscription = new SubscriptionDto
                {
                    UserId = subscription.UserId,
                    SubscriptionStatus = subscription.SubscriptionStatus,
                    PaymentVerifiedTimestamp = subscription.PaymentVerifiedTimestamp,
                    LastUpdated = subscription.Timestamp?.DateTime
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subscription for user {UserId}", userId);
            return StatusCode(500, new SubscriptionResponse
            {
                Success = false,
                Message = "Error retrieving subscription"
            });
        }
    }

    [HttpGet("admin/all")]
    [Authorize]
    [AdminAuthorize]
    public async Task<IActionResult> GetAllUsersWithSubscriptions()
    {
        try
        {
            var users = await _userRepository.GetAllUsersAsync();
            var usersWithSubscriptions = new List<object>();

            foreach (var user in users)
            {
                var subscription = await _subscriptionRepository.GetSubscriptionByUserIdAsync(user.RowKey);
                usersWithSubscriptions.Add(new
                {
                    userId = user.RowKey,
                    name = user.Name,
                    email = user.Email,
                    phone = user.Phone,
                    userType = user.UserType,
                    status = user.Status,
                    createdDate = user.CreatedDate,
                    subscriptionStatus = subscription?.SubscriptionStatus ?? "free",
                    paymentVerifiedTimestamp = subscription?.PaymentVerifiedTimestamp,
                    lastUpdated = subscription?.Timestamp?.DateTime
                });
            }

            return Ok(new
            {
                success = true,
                message = "Users retrieved successfully",
                users = usersWithSubscriptions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all users with subscriptions");
            return StatusCode(500, new
            {
                success = false,
                message = "Error retrieving users"
            });
        }
    }

    [HttpPost("update")]
    [Authorize]
    [AdminAuthorize]
    public async Task<IActionResult> UpdateSubscription([FromBody] UpdateSubscriptionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var user = await _userRepository.GetUserByIdAsync(request.UserId);
            if (user == null)
            {
                return NotFound(new SubscriptionResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var adminEmail = User.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? "unknown";

            var subscription = new SubscriptionEntity
            {
                UserId = request.UserId,
                SubscriptionStatus = request.SubscriptionStatus,
                PaymentVerifiedTimestamp = request.PaymentVerifiedTimestamp,
                UpdatedByAdmin = adminEmail
            };

            await _subscriptionRepository.CreateOrUpdateSubscriptionAsync(subscription);

            _logger.LogInformation(
                "Subscription updated for user {UserId} to {Status} by admin {AdminEmail}",
                request.UserId,
                request.SubscriptionStatus,
                adminEmail
            );

            return Ok(new SubscriptionResponse
            {
                Success = true,
                Message = "Subscription updated successfully",
                Subscription = new SubscriptionDto
                {
                    UserId = subscription.UserId,
                    SubscriptionStatus = subscription.SubscriptionStatus,
                    PaymentVerifiedTimestamp = subscription.PaymentVerifiedTimestamp,
                    LastUpdated = DateTime.UtcNow
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subscription for user {UserId}", request.UserId);
            return StatusCode(500, new SubscriptionResponse
            {
                Success = false,
                Message = "Error updating subscription"
            });
        }
    }
}
