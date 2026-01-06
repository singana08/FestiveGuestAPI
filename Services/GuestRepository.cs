using Azure.Data.Tables;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

public class GuestRepository : IGuestRepository
{
    private readonly TableClient _tableClient;

    public GuestRepository(TableClient tableClient)
    {
        _tableClient = tableClient;
    }

    public async Task<IEnumerable<GuestEntity>> GetAllGuestsAsync()
    {
        var guests = new List<GuestEntity>();
        await foreach (var guest in _tableClient.QueryAsync<GuestEntity>())
        {
            guests.Add(guest);
        }
        return guests;
    }

    public async Task<GuestEntity?> GetGuestAsync(string partitionKey, string rowKey)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<GuestEntity>(partitionKey, rowKey);
            return response.Value;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<GuestEntity> AddGuestAsync(GuestEntity guest)
    {
        await _tableClient.AddEntityAsync(guest);
        return guest;
    }

    public async Task<GuestEntity> UpdateGuestAsync(GuestEntity guest)
    {
        await _tableClient.UpdateEntityAsync(guest, guest.ETag);
        return guest;
    }

    public async Task DeleteGuestAsync(string partitionKey, string rowKey)
    {
        await _tableClient.DeleteEntityAsync(partitionKey, rowKey);
    }
}