using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services
{
    public interface IGuestPostRepository
    {
        Task<GuestPostEntity> CreateAsync(GuestPostEntity post);
        Task<List<GuestPostEntity>> GetAllActiveAsync();
        Task<GuestPostEntity?> GetByIdAsync(string id);
        Task<List<GuestPostEntity>> GetByUserIdAsync(string userId);
        Task<bool> DeleteAsync(string id, string userId);
        Task<GuestPostEntity> UpdateAsync(GuestPostEntity post);
    }
}