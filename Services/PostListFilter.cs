using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

/// <summary>Shared by the guest and host post feeds.</summary>
public static class PostListFilter
{
    /// <summary>
    /// Drops posts by members blocked in either direction or suspended by the team, flags posters whose
    /// phone is verified, and adds what the card shows about each poster (photos, rating, reply speed).
    /// placeOf: a listing's location, so its card gets the photos of its own cities (hosts' listings only).
    /// </summary>
    public static async Task<List<T>> PrepareFeedAsync<T>(
        HttpContext http, string userId, List<T> posts, Func<T, string> ownerOf, Action<T, bool> setVerified, Action<T, PosterInfo> setPoster,
        Func<T, string>? placeOf = null)
    {
        var services = http.RequestServices;
        var safety = services.GetRequiredService<ISafetyRepository>();
        var users = services.GetRequiredService<IUserRepository>();

        var hidden = await safety.GetHiddenUserIdsAsync(userId);
        // ponytail: scans the Users table on every feed request; store the flag on posts if this gets slow.
        var all = (await users.GetAllUsersAsync()).ToList();
        var byId = all.ToDictionary(u => u.RowKey);
        hidden.UnionWith(all.Where(u => u.Status == AccountStatus.Suspended).Select(u => u.RowKey));

        var visible = posts.Where(p => !hidden.Contains(ownerOf(p))).ToList();
        var gallery = services.GetRequiredService<IGalleryService>();
        var ratings = await services.GetRequiredService<IReviewService>().SummariesAsync();
        var badges = await services.GetRequiredService<IMemberStats>().ResponseBadgesAsync();
        var stays = await services.GetRequiredService<IStayService>().CompletedCountsAsync();

        foreach (var p in visible)
        {
            var owner = ownerOf(p);
            byId.TryGetValue(owner, out var user);
            setVerified(p, user?.IsVerified == true);
            var hostPhotos = user?.UserType == "Host" ? gallery.View(user) : new();
            var photos = placeOf == null ? hostPhotos : GalleryService.ForPlaces(hostPhotos, GalleryService.CitiesIn(placeOf(p)));
            var strip = photos.Take(12).ToList(); // the card shows 3; the rest let the page lead with a searched city's photos
            ratings.TryGetValue(owner, out var rating);
            setPoster(p, new PosterInfo
            {
                Photos = strip.Select(x => x.Url).ToList(),
                PhotoCities = strip.Select(x => x.City).ToList(),
                PhotoCount = photos.Count,
                Rating = rating.Average,
                ReviewCount = rating.Count,
                ResponseBadge = badges.GetValueOrDefault(owner),
                Stays = stays.GetValueOrDefault(owner),
                VerifiedHow = user?.AdminVerified == true ? user.AdminVerifiedHow : null,
                VerifiedSince = user?.AdminVerified == true ? user.AdminVerifiedAt : null,
            });
        }
        return visible;
    }
}
