using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services
{
    public interface IHostPostRepository
    {
        Task<HostPostEntity> CreateAsync(HostPostEntity post);
        Task<List<HostPostEntity>> GetAllActiveAsync();
        Task<HostPostEntity?> GetByIdAsync(string id);
        Task<List<HostPostEntity>> GetByUserIdAsync(string userId);
        Task<bool> DeleteAsync(string id, string userId);
    }
}