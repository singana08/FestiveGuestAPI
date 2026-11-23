using FestiveGuestAPI.Configuration;
using FestiveGuestAPI.Models;
using FestiveGuestAPI.Services;

namespace FestiveGuestAPI.BackgroundServices;

/// <summary>
/// Stay emails that go out on a schedule, each once per stay, to both people:
/// "your stay starts tomorrow" (with the guest's note for the host, safety tips for the guest),
/// and "how was your stay? leave a review" after it ends. Checks every 6 hours.
/// </summary>
public class StayReminderService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<StayReminderService> _logger;

    public StayReminderService(IServiceProvider services, ILogger<StayReminderService> logger)
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
                await SendStartRemindersAsync(scope.ServiceProvider);
                await SendDueAsync(scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stay reminders failed");
            }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private static string OtherName(StayEntity stay, string meId) => meId == stay.RequesterId ? stay.PartnerName : stay.RequesterName;

    /// <summary>Emails each participant who's still active; a failure for one person never stops the other.</summary>
    private static async Task<int> EmailBothAsync(IServiceProvider services, StayEntity stay, string what, Func<IEmailService, UserEntity, bool, Task> send)
    {
        var users = services.GetRequiredService<IUserRepository>();
        var email = services.GetRequiredService<IEmailService>();
        var sent = 0;
        foreach (var meId in new[] { stay.HostId, stay.GuestId })
        {
            var me = await users.GetUserByIdAsync(meId);
            if (me == null || me.Status != "Active" || string.IsNullOrEmpty(me.Email)) continue;
            try { await send(email, me, meId == stay.HostId); sent++; }
            catch (Exception ex)
            {
                services.GetService<ILogger<StayReminderService>>()?.LogWarning(ex, "Couldn't send the {What} email for stay {StayId} to {UserId}", what, stay.RowKey, meId);
            }
        }
        return sent;
    }

    /// <summary>"Your stay starts tomorrow".</summary>
    public static async Task<int> SendStartRemindersAsync(IServiceProvider services)
    {
        var stays = services.GetRequiredService<IStayService>();
        var baseUrl = services.GetRequiredService<AppSecrets>().AppBaseUrl;
        var sent = 0;
        foreach (var stay in await stays.DueForStartReminderAsync())
        {
            // Mark first: a mail-server hiccup means one missed reminder, never a repeat every 6 hours.
            await stays.MarkStartRemindedAsync(stay);
            var dates = StayService.Dates(stay.StartDate, stay.EndDate);
            sent += await EmailBothAsync(services, stay, "starts tomorrow", (email, me, isHost) =>
                email.SendStayTomorrowAsync(me.Email, me.Name, OtherName(stay, me.RowKey), stay.Kind, dates, isHost, isHost ? stay.Note : "",
                    isHost ? $"{baseUrl}/chats" : $"{baseUrl}/trips"));
        }
        return sent;
    }

    /// <summary>"How was your stay? Leave a review."</summary>
    public static async Task<int> SendDueAsync(IServiceProvider services)
    {
        var stays = services.GetRequiredService<IStayService>();
        var baseUrl = services.GetRequiredService<AppSecrets>().AppBaseUrl;
        var sent = 0;
        foreach (var stay in await stays.DueForReminderAsync())
        {
            await stays.MarkRemindedAsync(stay);
            sent += await EmailBothAsync(services, stay, "review reminder", (email, me, _) =>
            {
                var other = OtherName(stay, me.RowKey);
                return email.SendReviewReminderAsync(me.Email, me.Name, other, stay.Kind, $"{baseUrl}/profile/{Uri.EscapeDataString(other)}");
            });
        }
        return sent;
    }
}
