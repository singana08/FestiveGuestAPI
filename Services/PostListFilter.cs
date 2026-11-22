namespace FestiveGuestAPI.Services;

/// <summary>Shared by the guest and host post feeds.</summary>
public static class PostListFilter
{
    /// <summary>
    /// Drops posts by members blocked in either direction or suspended by the team,
    /// and flags posters whose phone is verified.
    /// </summary>
    public static async Task<List<T>> HideBlockedAndMarkVerifiedAsync<T>(
        HttpContext http, string userId, List<T> posts, Func<T, string> ownerOf, Action<T, bool> setVerified)
    {
        var safety = http.RequestServices.GetRequiredService<ISafetyRepository>();
        var users = http.RequestServices.GetRequiredService<IUserRepository>();

        var hidden = await safety.GetHiddenUserIdsAsync(userId);
        // ponytail: scans the Users table on every feed request; store the flag on posts if this gets slow.
        var all = (await users.GetAllUsersAsync()).ToList();
        var verified = all.Where(u => u.IsVerified).Select(u => u.RowKey).ToHashSet();
        hidden.UnionWith(all.Where(u => u.Status == AccountStatus.Suspended).Select(u => u.RowKey));

        var visible = posts.Where(p => !hidden.Contains(ownerOf(p))).ToList();
        foreach (var p in visible) setVerified(p, verified.Contains(ownerOf(p)));
        return visible;
    }
}
