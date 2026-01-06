using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

public interface IUserRepository
{
    Task<IEnumerable<UserEntity>> GetAllUsersAsync();
    Task<UserEntity?> GetUserByIdAsync(string userId);
    Task<UserEntity?> GetUserByEmailAsync(string email);
    Task<IEnumerable<UserEntity>> GetUsersByTypeAsync(string userType);
    Task<UserEntity> CreateUserAsync(UserEntity user);
    Task<UserEntity> UpdateUserAsync(UserEntity user);
}

public interface IOTPRepository
{
    Task<OTPEntity> CreateOTPAsync(OTPEntity otp);
    Task<OTPEntity?> GetValidOTPAsync(string email, string otpCode);
    Task MarkOTPAsUsedAsync(string email, string otpCode);
    Task CleanupExpiredOTPsAsync();
}

public interface ILocationRepository
{
    Task<IEnumerable<LocationEntity>> GetAllLocationsAsync();
    Task<IEnumerable<LocationEntity>> GetCitiesByStateAsync(string state);
    Task<IEnumerable<string>> GetStatesAsync();
}

public interface IChatThreadRepository
{
    Task<ChatThreadEntity?> GetChatThreadAsync(string user1Id, string user2Id);
    Task<ChatThreadEntity> CreateChatThreadAsync(ChatThreadEntity chatThread);
    Task<IEnumerable<ChatThreadEntity>> GetUserChatThreadsAsync(string userId);
}

public interface IPaymentRepository
{
    Task<IEnumerable<PaymentEntity>> GetUserPaymentsAsync(string userId);
    Task<IEnumerable<PaymentEntity>> GetPendingPaymentsAsync();
    Task<PaymentEntity> CreatePaymentAsync(PaymentEntity payment);
    Task<PaymentEntity> UpdatePaymentStatusAsync(PaymentEntity payment);
}