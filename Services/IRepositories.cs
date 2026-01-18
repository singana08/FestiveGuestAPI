using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

public interface IUserRepository
{
    Task<IEnumerable<UserEntity>> GetAllUsersAsync();
    Task<UserEntity?> GetUserByIdAsync(string userId);
    Task<UserEntity?> GetUserByEmailAsync(string email);
    Task<UserEntity?> GetUserByNameAsync(string name);
    Task<UserEntity?> GetUserByReferralCodeAsync(string referralCode);
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
    Task AddLocationAsync(LocationEntity location);
    Task DeleteLocationAsync(string state, string city);
}

public interface IPaymentRepository
{
    Task<IEnumerable<PaymentEntity>> GetUserPaymentsAsync(string userId);
    Task<IEnumerable<PaymentEntity>> GetPendingPaymentsAsync();
    Task<PaymentEntity> CreatePaymentAsync(PaymentEntity payment);
    Task<PaymentEntity> UpdatePaymentStatusAsync(PaymentEntity payment);
}

public interface ISubscriptionRepository
{
    Task<SubscriptionEntity?> GetSubscriptionByUserIdAsync(string userId);
    Task<SubscriptionEntity> CreateOrUpdateSubscriptionAsync(SubscriptionEntity subscription);
}