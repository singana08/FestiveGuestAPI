using System.Text.Json;
using System.Text.RegularExpressions;
using FestiveGuestAPI.DTOs;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

// City: one of the host's hosting cities, or null for "All places" (also what photos saved before cities existed are).
public record GalleryPhoto(string Id, string Caption, string? City = null);
public record GalleryPhotoView(string Id, string Caption, string Url, string? City = null);

/// <summary>
/// Photos of a host's places, so guests can see them before messaging. Each photo belongs to one of the host's
/// hosting cities or to "All places"; a listing card shows its own cities' photos first. Stored as private blobs
/// ({userId}_{photoId}.jpg in "host-gallery"); the list, captions and cities live on the user as JSON, cover first.
/// </summary>
public interface IGalleryService
{
    List<GalleryPhotoView> View(UserEntity user);
    Task<List<GalleryPhotoView>> GetAsync(string userId);
    Task<(string? PhotoId, string? UploadUrl, string? Error)> StartUploadAsync(string userId, string? city = null);
    Task<(List<GalleryPhotoView>? Photos, string? Error)> AddAsync(string userId, string photoId, string? caption, string? city = null);
    Task<(List<GalleryPhotoView>? Photos, string? Error)> UpdateAsync(string userId, List<GalleryPhoto> photos);
    Task<bool> RemoveAsync(string userId, string photoId);
}

public class GalleryService : IGalleryService
{
    public const string Container = "host-gallery";
    public const int PerPlace = 6; // per hosting city, and for "All places"
    public const int Limit = 30;   // ponytail: one overall cap for everyone; FestiveGuest Plus (payments phase) can raise both
    public const int CaptionMax = 80;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IUserRepository _users;
    private readonly IFileUploadService _files;

    public GalleryService(IUserRepository users, IFileUploadService files)
    {
        _users = users;
        _files = files;
    }

    public static List<GalleryPhoto> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<GalleryPhoto>>(json, Json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    public static string BlobName(string userId, string photoId) => $"{userId}_{photoId}.jpg";

    private static string Clean(string? caption)
    {
        var c = (caption ?? "").Trim();
        return c.Length > CaptionMax ? c[..CaptionMax] : c;
    }

    private static string LimitMessage(string? city) => city == null
        ? $"You can add up to {PerPlace} photos for all places. Remove one to add another."
        : $"You can add up to {PerPlace} photos for {city}. Remove one to add another.";
    private static string TotalMessage => $"You can add up to {Limit} photos in all. Remove one to add another.";

    private static bool SamePlace(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>The cities on the host's profile (hosting areas), case-insensitive.</summary>
    public static HashSet<string> HostingCities(UserEntity user)
    {
        var cities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(user.HostingAreas)) return cities;
        try
        {
            foreach (var area in JsonSerializer.Deserialize<List<HostingAreaDto>>(user.HostingAreas, Json) ?? new())
                foreach (var c in area.Cities) if (!string.IsNullOrWhiteSpace(c)) cities.Add(c.Trim());
        }
        catch (JsonException) { }
        return cities;
    }

    /// <summary>null for "All places"; otherwise the hosting city as written on their profile, or an error if it isn't one of theirs.</summary>
    private static (string? City, string? Error) Place(UserEntity host, string? city)
    {
        if (string.IsNullOrWhiteSpace(city)) return (null, null);
        return HostingCities(host).TryGetValue(city.Trim(), out var match) ? (match, null) : (null, "Pick one of the cities you host in.");
    }

    /// <summary>The cities in a listing's location, "Eluru, Guntur (Andhra Pradesh) | Hyderabad (Telangana)" — same rule as the website.</summary>
    public static List<string> CitiesIn(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return new();
        if (location.Contains('('))
            return location.Split('|').SelectMany(part => Regex.Replace(part, @"\(.*?\)", "").Split(',')).Select(s => s.Trim()).Where(s => s != "").ToList();
        return new[] { location.Split(',')[0].Trim() }.Where(s => s != "").ToList();
    }

    /// <summary>What a listing card shows: the photos of the listing's own cities (in its order), then the "All places" ones.</summary>
    public static List<GalleryPhotoView> ForPlaces(List<GalleryPhotoView> all, IEnumerable<string> cities)
    {
        var own = cities.SelectMany(c => all.Where(p => p.City != null && SamePlace(p.City, c))).Distinct();
        return own.Concat(all.Where(p => p.City == null)).ToList();
    }

    public List<GalleryPhotoView> View(UserEntity user) =>
        Parse(user.Gallery).Select(p => new GalleryPhotoView(p.Id, p.Caption, _files.GenerateReadSasUrl(BlobName(user.RowKey, p.Id), Container), p.City)).ToList();

    public async Task<List<GalleryPhotoView>> GetAsync(string userId)
    {
        var user = await _users.GetUserByIdAsync(userId);
        return user == null ? new() : View(user);
    }

    private async Task<(UserEntity? User, string? Error)> HostAsync(string userId)
    {
        var user = await _users.GetUserByIdAsync(userId);
        if (user == null) return (null, "Member not found.");
        if (user.UserType != "Host") return (null, "Only hosts can add photos of their place.");
        return (user, null);
    }

    private async Task<List<GalleryPhotoView>> SaveAsync(UserEntity user, List<GalleryPhoto> photos)
    {
        user.Gallery = photos.Count == 0 ? "" : JsonSerializer.Serialize(photos, Json);
        await _users.UpdateUserAsync(user);
        return View(user);
    }

    public async Task<(string? PhotoId, string? UploadUrl, string? Error)> StartUploadAsync(string userId, string? city = null)
    {
        var (user, error) = await HostAsync(userId);
        if (user == null) return (null, null, error);
        var (place, placeError) = Place(user, city);
        if (placeError != null) return (null, null, placeError);
        var photos = Parse(user.Gallery);
        if (photos.Count >= Limit) return (null, null, TotalMessage);
        if (photos.Count(p => SamePlace(p.City, place)) >= PerPlace) return (null, null, LimitMessage(place));
        var photoId = Guid.NewGuid().ToString("N");
        return (photoId, await _files.GenerateUploadUrlAsync(Container, BlobName(userId, photoId)), null);
    }

    public async Task<(List<GalleryPhotoView>? Photos, string? Error)> AddAsync(string userId, string photoId, string? caption, string? city = null)
    {
        if (!Guid.TryParseExact(photoId, "N", out _)) return (null, "That photo wasn't found.");
        var (user, error) = await HostAsync(userId);
        if (user == null) return (null, error);

        var photos = Parse(user.Gallery);
        if (photos.Any(p => p.Id == photoId)) return (View(user), null); // added already (a retried request)
        var (place, placeError) = Place(user, city);
        if (placeError != null) return (null, placeError);
        if (photos.Count >= Limit) return (null, TotalMessage);
        if (photos.Count(p => SamePlace(p.City, place)) >= PerPlace) return (null, LimitMessage(place));
        if (!await _files.BlobExistsAsync(Container, BlobName(userId, photoId)))
            return (null, "That photo didn't finish uploading. Please try again.");

        photos.Add(new GalleryPhoto(photoId, Clean(caption), place));
        return (await SaveAsync(user, photos), null);
    }

    /// <summary>New order, captions and places. Must be the same photos — adding and removing have their own calls.</summary>
    public async Task<(List<GalleryPhotoView>? Photos, string? Error)> UpdateAsync(string userId, List<GalleryPhoto> photos)
    {
        var (user, error) = await HostAsync(userId);
        if (user == null) return (null, error);

        var current = Parse(user.Gallery).Select(p => p.Id).ToHashSet();
        var wanted = photos.Select(p => p.Id).ToList();
        if (wanted.Count != current.Count || wanted.Distinct().Count() != wanted.Count || !wanted.All(current.Contains))
            return (null, "Your photos changed in another window. Please reload and try again.");

        // A photo moved to another place must go to one of their cities; one that stays put keeps its city,
        // even if that city has since left their profile.
        var was = Parse(user.Gallery).ToDictionary(p => p.Id, p => p.City);
        var next = new List<GalleryPhoto>();
        foreach (var p in photos)
        {
            var place = was[p.Id];
            var asked = string.IsNullOrWhiteSpace(p.City) ? null : p.City.Trim();
            if (!SamePlace(asked, place))
            {
                var (moved, placeError) = Place(user, asked);
                if (placeError != null) return (null, placeError);
                place = moved;
            }
            next.Add(new GalleryPhoto(p.Id, Clean(p.Caption), place));
        }
        var crowded = next.GroupBy(p => p.City?.ToLowerInvariant()).FirstOrDefault(g => g.Count() > PerPlace);
        if (crowded != null) return (null, LimitMessage(crowded.First().City));

        return (await SaveAsync(user, next), null);
    }

    /// <summary>Removes the photo from the gallery and deletes the file. Used by the host and by admins.</summary>
    public async Task<bool> RemoveAsync(string userId, string photoId)
    {
        var user = await _users.GetUserByIdAsync(userId);
        if (user == null) return false;
        var photos = Parse(user.Gallery);
        if (photos.RemoveAll(p => p.Id == photoId) == 0) return false;
        await SaveAsync(user, photos);
        await _files.DeleteBlobAsync(Container, BlobName(userId, photoId));
        return true;
    }
}
