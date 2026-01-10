using Azure.Data.Tables;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

public class UserRepository : IUserRepository
{
    private readonly TableClient _tableClient;

    public UserRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient("Users");
    }

    public async Task<IEnumerable<UserEntity>> GetAllUsersAsync()
    {
        var users = new List<UserEntity>();
        await foreach (var user in _tableClient.QueryAsync<UserEntity>())
        {
            users.Add(user);
        }
        return users;
    }

    public async Task<UserEntity?> GetUserByIdAsync(string userId)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<UserEntity>("USER", userId);
            return response.Value;
        }
        catch
        {
            return null;
        }
    }

    public async Task<UserEntity?> GetUserByEmailAsync(string email)
    {
        await foreach (var user in _tableClient.QueryAsync<UserEntity>(u => u.Email == email))
        {
            return user;
        }
        return null;
    }

    public async Task<UserEntity?> GetUserByNameAsync(string name)
    {
        await foreach (var user in _tableClient.QueryAsync<UserEntity>(u => u.Name.ToLower() == name.ToLower()))
        {
            return user;
        }
        return null;
    }

    public async Task<UserEntity?> GetUserByReferralCodeAsync(string referralCode)
    {
        await foreach (var user in _tableClient.QueryAsync<UserEntity>(u => u.ReferralCode == referralCode))
        {
            return user;
        }
        return null;
    }

    public async Task<IEnumerable<UserEntity>> GetUsersByTypeAsync(string userType)
    {
        var users = new List<UserEntity>();
        await foreach (var user in _tableClient.QueryAsync<UserEntity>(u => u.UserType == userType))
        {
            users.Add(user);
        }
        return users;
    }

    public async Task<UserEntity> CreateUserAsync(UserEntity user)
    {
        user.PartitionKey = "USER";
        user.RowKey = Guid.NewGuid().ToString();
        user.CreatedDate = DateTime.UtcNow;
        user.Status = "Active";
        user.IsVerified = true;
        user.ReferralCode = GenerateReferralCode(user.RowKey);
        
        await _tableClient.AddEntityAsync(user);
        return user;
    }

    private string GenerateReferralCode(string userId)
    {
        var random = new Random();
        var randomPart = random.Next(1000, 9999);
        return $"FG{userId.Substring(0, Math.Min(5, userId.Length))}{randomPart}".ToUpper();
    }

    public async Task<UserEntity> UpdateUserAsync(UserEntity user)
    {
        await _tableClient.UpdateEntityAsync(user, user.ETag);
        return user;
    }
}