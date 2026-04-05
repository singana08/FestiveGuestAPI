using System.Text.Json;
using FestiveGuestAPI.DTOs;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

public interface INotificationService
{
    Task NotifyHostsAboutGuestPostAsync(GuestPostEntity post, bool isUpdate = false);
    Task NotifyGuestsAboutHostPostAsync(HostPostEntity post, bool isUpdate = false);
}

public class NotificationService : INotificationService
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationService> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public NotificationService(IUserRepository userRepository, IEmailService emailService, ILogger<NotificationService> logger)
    {
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task NotifyHostsAboutGuestPostAsync(GuestPostEntity post, bool isUpdate = false)
    {
        try
        {
            var hosts = await _userRepository.GetUsersByTypeAsync("Host");
            var postLocation = post.Location.ToLower();

            foreach (var host in hosts.Where(h => h.Status == "Active"))
            {
                if (!IsEmailNotificationEnabled(host) || !HostMatchesLocation(host, postLocation))
                    continue;

                try
                {
                    await _emailService.SendGuestPostNotificationAsync(
                        host.Email, host.Name, post.UserName, post.Title, post.Location, post.RowKey, isUpdate);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send guest post notification to {Email}", host.Email);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process guest post notifications for post {PostId}", post.RowKey);
        }
    }

    public async Task NotifyGuestsAboutHostPostAsync(HostPostEntity post, bool isUpdate = false)
    {
        try
        {
            var guests = await _userRepository.GetUsersByTypeAsync("Guest");
            var postLocation = post.Location.ToLower();

            foreach (var guest in guests.Where(g => g.Status == "Active"))
            {
                if (!IsEmailNotificationEnabled(guest))
                    continue;

                // Match if guest's location contains or is contained by the post location
                if (!string.IsNullOrEmpty(guest.Location) &&
                    (guest.Location.ToLower().Contains(postLocation) || postLocation.Contains(guest.Location.ToLower())))
                {
                    try
                    {
                        await _emailService.SendHostPostNotificationAsync(
                            guest.Email, guest.Name, post.UserName, post.Title, post.Location, post.RowKey, isUpdate);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send host post notification to {Email}", guest.Email);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process host post notifications for post {PostId}", post.RowKey);
        }
    }

    private static bool IsEmailNotificationEnabled(UserEntity user)
    {
        if (string.IsNullOrEmpty(user.NotificationPreferences))
            return true; // default to enabled

        try
        {
            var prefs = JsonSerializer.Deserialize<NotificationPreferencesDto>(user.NotificationPreferences, _jsonOptions);
            return prefs?.Email ?? true;
        }
        catch
        {
            return true;
        }
    }

    private static bool HostMatchesLocation(UserEntity host, string postLocation)
    {
        if (string.IsNullOrEmpty(host.HostingAreas))
            return false;

        try
        {
            var hostingAreas = JsonSerializer.Deserialize<List<HostingAreaDto>>(host.HostingAreas, _jsonOptions);
            if (hostingAreas == null) return false;

            return hostingAreas.SelectMany(h => h.Cities.Select(c => c.ToLower()))
                               .Any(city => postLocation.Contains(city));
        }
        catch
        {
            return false;
        }
    }
}
