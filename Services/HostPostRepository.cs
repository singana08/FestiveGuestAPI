using Azure.Data.Tables;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services
{
    public class HostPostRepository : IHostPostRepository
    {
        private readonly TableClient _tableClient;

        public HostPostRepository(TableServiceClient tableServiceClient)
        {
            _tableClient = tableServiceClient.GetTableClient("HostPosts");
            _tableClient.CreateIfNotExists();
        }

        public async Task<HostPostEntity> CreateAsync(HostPostEntity post)
        {
            var response = await _tableClient.AddEntityAsync(post);
            return post;
        }

        public async Task<List<HostPostEntity>> GetAllActiveAsync()
        {
            var posts = new List<HostPostEntity>();
            
            await foreach (var post in _tableClient.QueryAsync<HostPostEntity>(
                filter: $"PartitionKey eq 'HostPost' and Status eq 'Active'"))
            {
                posts.Add(post);
            }

            return posts.OrderByDescending(p => p.CreatedAt).ToList();
        }

        public async Task<HostPostEntity?> GetByIdAsync(string id)
        {
            try
            {
                var response = await _tableClient.GetEntityAsync<HostPostEntity>("HostPost", id);
                return response.Value;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task<List<HostPostEntity>> GetByUserIdAsync(string userId)
        {
            var posts = new List<HostPostEntity>();
            
            await foreach (var post in _tableClient.QueryAsync<HostPostEntity>(
                filter: $"PartitionKey eq 'HostPost' and UserId eq '{userId}'"))
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

        public async Task<HostPostEntity> UpdateAsync(HostPostEntity post)
        {
            await _tableClient.UpdateEntityAsync(post, post.ETag);
            return post;
        }
    }
}