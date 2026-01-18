using Azure.Data.Tables;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly TableClient _tableClient;

    public SubscriptionRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient("Subscriptions");
        _tableClient.CreateIfNotExists();
    }

    public async Task<SubscriptionEntity?> GetSubscriptionByUserIdAsync(string userId)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<SubscriptionEntity>("Subscription", userId);
            return response.Value;
        }
        catch
        {
            return null;
        }
    }

    public async Task<SubscriptionEntity> CreateOrUpdateSubscriptionAsync(SubscriptionEntity subscription)
    {
        subscription.PartitionKey = "Subscription";
        subscription.RowKey = subscription.UserId;
        await _tableClient.UpsertEntityAsync(subscription);
        return subscription;
    }
}
