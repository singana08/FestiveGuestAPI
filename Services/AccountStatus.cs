using Microsoft.Extensions.Caching.Memory;

namespace FestiveGuestAPI.Services;

/// <summary>
/// Sign-in tokens last 30 days, so suspension is also checked on every authenticated request
/// (see OnTokenValidated in Program.cs). Only an explicit "Suspended" status turns a member away,
/// so a storage hiccup never signs people out.
/// </summary>
public static class AccountStatus
{
    public const string Suspended = "Suspended";
    private static string Key(string userId) => $"account-suspended:{userId}";

    public static async Task<bool> IsSuspendedAsync(IServiceProvider services, string userId)
    {
        var cache = services.GetRequiredService<IMemoryCache>();
        if (cache.TryGetValue(Key(userId), out bool suspended)) return suspended;

        var user = await services.GetRequiredService<IUserRepository>().GetUserByIdAsync(userId);
        suspended = user?.Status == Suspended;
        // ponytail: per-instance cache; with several API instances a status change can take up to a minute to reach all of them.
        cache.Set(Key(userId), suspended, TimeSpan.FromMinutes(1));
        return suspended;
    }

    /// <summary>Call after changing a member's status so it applies on their next request.</summary>
    public static void Forget(IServiceProvider services, string userId) =>
        services.GetRequiredService<IMemoryCache>().Remove(Key(userId));
}
