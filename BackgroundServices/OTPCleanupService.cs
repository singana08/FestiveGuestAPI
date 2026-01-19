using FestiveGuestAPI.Services;

namespace FestiveGuestAPI.BackgroundServices;

public class OTPCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OTPCleanupService> _logger;

    public OTPCleanupService(IServiceProvider serviceProvider, ILogger<OTPCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var otpRepository = scope.ServiceProvider.GetRequiredService<IOTPRepository>();
                
                await otpRepository.CleanupExpiredOTPsAsync();
                _logger.LogInformation("OTP cleanup completed at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OTP cleanup");
            }

            // Run every 1 hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
