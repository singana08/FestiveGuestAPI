using Azure.Data.Tables;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services
{
    public class GuestPostRepository : IGuestPostRepository
    {
        private readonly TableClient _tableClient;

        public GuestPostRepository(TableServiceClient tableServiceClient)
        {
            _tableClient = tableServiceClient.GetTableClient("GuestPosts");
            _tableClient.CreateIfNotExists();
        }

        public async Task<GuestPostEntity> CreateAsync(GuestPostEntity post)
        {
            var response = await _tableClient.AddEntityAsync(post);
            return post;
        }

        public async Task<List<GuestPostEntity>> GetAllActiveAsync()
        {
            var posts = new List<GuestPostEntity>();
            
            await foreach (var post in _tableClient.QueryAsync<GuestPostEntity>(
                filter: $"PartitionKey eq 'GuestPost' and Status eq 'Active'"))
            {
                posts.Add(post);
            }

            return posts.OrderByDescending(p => p.CreatedAt).ToList();
        }

        public async Task<GuestPostEntity?> GetByIdAsync(string id)
        {
            try
            {
                var response = await _tableClient.GetEntityAsync<GuestPostEntity>("GuestPost", id);
                return response.Value;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task<List<GuestPostEntity>> GetByUserIdAsync(string userId)
        {
            var posts = new List<GuestPostEntity>();
            
            await foreach (var post in _tableClient.QueryAsync<GuestPostEntity>(
                filter: $"PartitionKey eq 'GuestPost' and UserId eq '{userId}'"))
            {
                posts.Add(post);
            }

            return posts.OrderByDescending(p => p.CreatedAt).ToList();
        }

        public async Task<bool> DeleteAsync(string id, string userId)
        {
            try
            {
                var post = await GetByIdAsync(id);
                if (post == null || post.UserId != userId)
                {
                    return false;
                }

                post.Status = "Deleted";
                post.UpdatedAt = DateTime.UtcNow;
                
                await _tableClient.UpdateEntityAsync(post, post.ETag);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}