using Azure.Data.Tables;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

public class OTPRepository : IOTPRepository
{
    private readonly TableClient _tableClient;

    public OTPRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient("OTPs");
    }

    public async Task<OTPEntity> CreateOTPAsync(OTPEntity otp)
    {
        otp.PartitionKey = "OTP";
        otp.RowKey = $"{otp.Email}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        
        await _tableClient.AddEntityAsync(otp);
        return otp;
    }

    public async Task<OTPEntity?> GetValidOTPAsync(string email, string otpCode)
    {
        await foreach (var otp in _tableClient.QueryAsync<OTPEntity>(
            o => o.Email == email && o.OTPCode == otpCode && !o.IsUsed))
        {
            if (otp.ExpirationTime > DateTime.UtcNow)
            {
                return otp;
            }
        }
        return null;
    }

    public async Task MarkOTPAsUsedAsync(string email, string otpCode)
    {
        await foreach (var otp in _tableClient.QueryAsync<OTPEntity>(
            o => o.Email == email && o.OTPCode == otpCode && !o.IsUsed))
        {
            // Delete OTP after successful use instead of marking as used
            await _tableClient.DeleteEntityAsync(otp.PartitionKey, otp.RowKey);
            break;
        }
    }

    public async Task CleanupExpiredOTPsAsync()
    {
        var expiredOTPs = new List<OTPEntity>();
        await foreach (var otp in _tableClient.QueryAsync<OTPEntity>())
        {
            // Delete expired or used OTPs
            if (otp.ExpirationTime < DateTime.UtcNow || otp.IsUsed)
            {
                expiredOTPs.Add(otp);
            }
        }

        foreach (var otp in expiredOTPs)
        {
            await _tableClient.DeleteEntityAsync(otp.PartitionKey, otp.RowKey);
        }
    }
}