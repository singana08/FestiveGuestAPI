using Azure.Data.Tables;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

public interface IReferralPointsService
{
    Task AwardPointsForReferralAsync(string referrerUserId, string referredUserId);
    Task<bool> RedeemPointsForSubscriptionAsync(string userId);
    Task AdjustPointsAsync(string userId, int points, string description, string adminEmail);
    Task<List<PointsTransactionEntity>> GetPointsHistoryAsync(string userId);
}

public class ReferralPointsService : IReferralPointsService
{
    private readonly IUserRepository _userRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly TableClient _transactionsTableClient;
    private const int POINTS_PER_REFERRAL = 100;
    private const int POINTS_FOR_SUBSCRIPTION = 500;
    private const int SUBSCRIPTION_MONTHS = 3;

    public ReferralPointsService(
        IUserRepository userRepository,
        ISubscriptionRepository subscriptionRepository,
        TableServiceClient tableServiceClient)
    {
        _userRepository = userRepository;
        _subscriptionRepository = subscriptionRepository;
        _transactionsTableClient = tableServiceClient.GetTableClient("PointsTransactions");
        _transactionsTableClient.CreateIfNotExists();
    }

    public async Task AwardPointsForReferralAsync(string referrerUserId, string referredUserId)
    {
        var referrer = await _userRepository.GetUserByIdAsync(referrerUserId);
        if (referrer == null) return;

        referrer.ReferralPoints += POINTS_PER_REFERRAL;
        await _userRepository.UpdateUserAsync(referrer);

        await LogTransactionAsync(referrerUserId, POINTS_PER_REFERRAL, "earned", 
            $"Earned {POINTS_PER_REFERRAL} points for referring user {referredUserId}");
    }

    public async Task<bool> RedeemPointsForSubscriptionAsync(string userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null) return false;

        if (user.ReferralPoints < POINTS_FOR_SUBSCRIPTION)
            return false;

        var subscription = await _subscriptionRepository.GetSubscriptionByUserIdAsync(userId);
        if (subscription != null && subscription.SubscriptionStatus == "paid")
            return false;

        user.ReferralPoints -= POINTS_FOR_SUBSCRIPTION;
        await _userRepository.UpdateUserAsync(user);

        var expiryDate = DateTime.UtcNow.AddMonths(SUBSCRIPTION_MONTHS);
        var subscriptionEntity = new SubscriptionEntity
        {
            UserId = userId,
            SubscriptionStatus = "paid",
            PaymentVerifiedTimestamp = DateTime.UtcNow,
            UpdatedByAdmin = "System (Points Redemption)",
            PaymentMethod = "points"
        };
        await _subscriptionRepository.CreateOrUpdateSubscriptionAsync(subscriptionEntity);

        await LogTransactionAsync(userId, -POINTS_FOR_SUBSCRIPTION, "redeemed", 
            $"Redeemed {POINTS_FOR_SUBSCRIPTION} points for {SUBSCRIPTION_MONTHS} months subscription");

        return true;
    }

    public async Task AdjustPointsAsync(string userId, int points, string description, string adminEmail)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null) return;

        user.ReferralPoints += points;
        if (user.ReferralPoints < 0) user.ReferralPoints = 0;
        
        await _userRepository.UpdateUserAsync(user);

        await LogTransactionAsync(userId, points, "admin_adjustment", 
            $"{description} (by {adminEmail})");
    }

    public async Task<List<PointsTransactionEntity>> GetPointsHistoryAsync(string userId)
    {
        var transactions = new List<PointsTransactionEntity>();
        await foreach (var transaction in _transactionsTableClient.QueryAsync<PointsTransactionEntity>(
            t => t.PartitionKey == userId))
        {
            transactions.Add(transaction);
        }
        return transactions.OrderByDescending(t => t.CreatedDate).ToList();
    }

    private async Task LogTransactionAsync(string userId, int points, string type, string description)
    {
        var transaction = new PointsTransactionEntity
        {
            PartitionKey = userId,
            RowKey = Guid.NewGuid().ToString(),
            UserId = userId,
            Points = points,
            Type = type,
            Description = description,
            CreatedDate = DateTime.UtcNow
        };

        await _transactionsTableClient.AddEntityAsync(transaction);
    }
}
