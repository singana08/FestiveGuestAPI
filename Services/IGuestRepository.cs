using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

public interface IGuestRepository
{
    Task<IEnumerable<GuestEntity>> GetAllGuestsAsync();
    Task<GuestEntity?> GetGuestAsync(string partitionKey, string rowKey);
    Task<GuestEntity> AddGuestAsync(GuestEntity guest);
    Task<GuestEntity> UpdateGuestAsync(GuestEntity guest);
    Task DeleteGuestAsync(string partitionKey, string rowKey);
}