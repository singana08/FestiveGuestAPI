using FestiveGuestAPI.Configuration;
using FestiveGuestAPI.Services;

namespace FestiveGuestAPI.BackgroundServices;

/// <summary>Once a confirmed stay has ended, emails both people once: "How was your stay? Leave a review."</summary>
public class ReviewReminderService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ReviewReminderService> _logger;

    public ReviewReminderService(IServiceProvider services, ILogger<ReviewReminderService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                await SendDueAsync(scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Review reminders failed");
            }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    public static async Task<int> SendDueAsync(IServiceProvider services)
    {
        var stays = services.GetRequiredService<IStayService>();
        var users = services.GetRequiredService<IUserRepository>();
        var email = services.GetRequiredService<IEmailService>();
        var baseUrl = services.GetRequiredService<AppSecrets>().AppBaseUrl;
        var sent = 0;

        foreach (var stay in await stays.DueForReminderAsync())
        {
            // Mark first: a mail-server hiccup means one missed reminder, never a daily repeat.
            await stays.MarkRemindedAsync(stay);
            foreach (var (meId, otherName) in new[] { (stay.HostId, stay.HostId == stay.RequesterId ? stay.PartnerName : stay.RequesterName),
                                                      (stay.GuestId, stay.GuestId == stay.RequesterId ? stay.PartnerName : stay.RequesterName) })
            {
                var me = await users.GetUserByIdAsync(meId);
                if (me == null || me.Status != "Active" || string.IsNullOrEmpty(me.Email)) continue;
                try
                {
                    await email.SendReviewReminderAsync(me.Email, me.Name, otherName, stay.Kind, $"{baseUrl}/profile/{Uri.EscapeDataString(otherName)}");
                    sent++;
                }
                catch (Exception ex)
                {
                    services.GetService<ILogger<ReviewReminderService>>()?.LogWarning(ex, "Couldn't send the review reminder for stay {StayId} to {UserId}", stay.RowKey, meId);
                }
            }
        }
        return sent;
    }
}
