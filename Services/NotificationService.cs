using System.Text.Json;
using FestiveGuestAPI.DTOs;
using FestiveGuestAPI.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FestiveGuestAPI.Services;

public interface INotificationService
{
    Task NotifyHostsAboutGuestPostAsync(GuestPostEntity post, bool isUpdate = false);
    Task NotifyGuestsAboutHostPostAsync(HostPostEntity post, bool isUpdate = false);
    Task NotifySignInAsync(UserDto user, string? userAgent, string method);
    Task NotifyPasswordChangedAsync(UserEntity? user, string? userAgent);
    Task NotifyNewMessageAsync(string senderId, string senderName, string receiverId);
}

public class NotificationService : INotificationService
{
    /// <summary>At most one "X messaged you" email per sender per this long, so a chat isn't an inbox flood.</summary>
    public static readonly TimeSpan MessageEmailGap = TimeSpan.FromHours(6);

    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NotificationService> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public NotificationService(IUserRepository userRepository, IEmailService emailService, IMemoryCache cache, ILogger<NotificationService> logger)
    {
        _userRepository = userRepository;
        _emailService = emailService;
        _cache = cache;
        _logger = logger;
    }

    public async Task NotifySignInAsync(UserDto user, string? userAgent, string method)
    {
        if (string.IsNullOrEmpty(user.Email) || !user.NotificationPreferences.SignIns) return;
        try { await _emailService.SendSecurityAlertAsync(user.Email, user.Name, $"New sign-in with {method}", DeviceName(userAgent)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to send sign-in alert to {UserId}", user.UserId); }
    }

    public async Task NotifyPasswordChangedAsync(UserEntity? user, string? userAgent)
    {
        if (user == null || string.IsNullOrEmpty(user.Email) || !NotificationPreferencesDto.Parse(user.NotificationPreferences).SignIns) return;
        try { await _emailService.SendSecurityAlertAsync(user.Email, user.Name, "Your password was changed", DeviceName(userAgent)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to send password-changed alert to {UserId}", user.RowKey); }
    }

    public async Task NotifyNewMessageAsync(string senderId, string senderName, string receiverId)
    {
        // ponytail: per-instance memory, so with several App Service instances a member can get one extra email.
        var key = $"message-email:{receiverId}:{senderId}";
        if (_cache.TryGetValue(key, out _)) return;
        try
        {
            var receiver = await _userRepository.GetUserByIdAsync(receiverId);
            if (receiver == null || receiver.Status != "Active" || string.IsNullOrEmpty(receiver.Email)
                || !NotificationPreferencesDto.Parse(receiver.NotificationPreferences).Messages)
                return;
            _cache.Set(key, true, MessageEmailGap);
            await _emailService.SendNewMessageEmailAsync(receiver.Email, receiver.Name, senderName);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to send new-message email to {UserId}", receiverId); }
    }

    /// <summary>"Chrome on Windows" from a User-Agent — enough for someone to recognise their own device.</summary>
    public static string DeviceName(string? userAgent)
    {
        var ua = userAgent ?? "";
        var browser = ua.Contains("Edg/") ? "Edge" : ua.Contains("OPR/") ? "Opera" : ua.Contains("SamsungBrowser") ? "Samsung Internet"
            : ua.Contains("Chrome/") || ua.Contains("CriOS/") ? "Chrome" : ua.Contains("Firefox/") || ua.Contains("FxiOS/") ? "Firefox"
            : ua.Contains("Safari/") ? "Safari" : "a browser";
        var device = ua.Contains("Android") ? "Android" : ua.Contains("iPhone") ? "iPhone" : ua.Contains("iPad") ? "iPad"
            : ua.Contains("Windows") ? "Windows" : ua.Contains("Mac OS") ? "Mac" : ua.Contains("Linux") ? "Linux" : "an unknown device";
        return $"{browser} on {device}";
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
                        host.Email, host.Name, post.UserName, post.Title, post.Location, post.RowKey, isUpdate, post.GuideSummary());
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

    private static bool IsEmailNotificationEnabled(UserEntity user) =>
        NotificationPreferencesDto.Parse(user.NotificationPreferences).Email;

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
