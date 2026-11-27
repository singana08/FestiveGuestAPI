using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using FestiveGuestAPI.Models;
using FestiveGuestAPI.Services;

namespace FestiveGuestAPI.Controllers;

/// <summary>
/// What a signed-out visitor may see before joining: hosts' listings (first name, place, price, one photo, rating, badges)
/// and how many upcoming trip requests each city has. Trips are counts only — a stranger shouldn't learn who is
/// travelling where and when. Messaging, profiles and full posts still need an account.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private const int MaxListings = 60;

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(
        [FromServices] IHostPostRepository hostPosts, [FromServices] IGuestPostRepository guestPosts, [FromServices] IMemoryCache cache)
    {
        // Anyone can call this, so it's built at most once every 5 minutes (photo links last an hour).
        var preview = await cache.GetOrCreateAsync("public-preview", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await Build(HttpContext, hostPosts, guestPosts, DateTime.UtcNow);
        });
        return Ok(preview);
    }

    /// <summary>The preview itself; public for tests.</summary>
    public static async Task<object> Build(HttpContext http, IHostPostRepository hostPosts, IGuestPostRepository guestPosts, DateTime now)
    {
        // "" = no viewer: nobody's blocks apply, but suspended members are still left out.
        var listings = await PostListFilter.PrepareFeedAsync(http, "", await hostPosts.GetAllActiveAsync(),
            p => p.UserId, (p, v) => p.UserVerified = v, (p, info) => p.Poster = info, p => p.Location);
        var trips = await PostListFilter.PrepareFeedAsync(http, "", await guestPosts.GetAllActiveAsync(),
            p => p.UserId, (p, v) => p.UserVerified = v, (p, info) => p.Poster = info);

        var hosts = listings
            .OrderByDescending(p => p.CreatedAt)
            .Take(MaxListings)
            .Select(p => new
            {
                id = p.RowKey,
                name = FirstName(p.UserName),
                location = p.Location,
                title = p.Title,
                pricePerNight = p.PricePerNight,
                maxGuests = p.MaxGuests,
                amenities = p.Amenities,
                availableFrom = p.CommencementDate,
                photo = p.Poster?.Photos.FirstOrDefault(),
                rating = p.Poster?.Rating ?? 0,
                reviewCount = p.Poster?.ReviewCount ?? 0,
                phoneVerified = p.UserVerified,
                teamVerified = p.Poster?.VerifiedHow != null,
            })
            .ToList();

        var tripsByCity = trips
            .Where(t => t.VisitingDate == null || t.VisitingDate.Value.Date >= now.Date)
            .GroupBy(t => City(t.Location), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Key.Length > 0)
            .Select(g => new { city = g.Key, count = g.Count() })
            .OrderByDescending(g => g.count)
            .ToList();

        return new { hosts, trips = tripsByCity };
    }

    private static string FirstName(string? name) => (name ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    private static string City(string? location) => (location ?? "").Split(',')[0].Trim();
}
