using System.Text.Json;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

public record GalleryPhoto(string Id, string Caption);
public record GalleryPhotoView(string Id, string Caption, string Url);

/// <summary>
/// Photos of a host's place, so guests can see it before messaging. Stored as private blobs
/// ({userId}_{photoId}.jpg in "host-gallery"); the list and captions live on the user as JSON, cover first.
/// </summary>
public interface IGalleryService
{
    List<GalleryPhotoView> View(UserEntity user);
    Task<List<GalleryPhotoView>> GetAsync(string userId);
    Task<(string? PhotoId, string? UploadUrl, string? Error)> StartUploadAsync(string userId);
    Task<(List<GalleryPhotoView>? Photos, string? Error)> AddAsync(string userId, string photoId, string? caption);
    Task<(List<GalleryPhotoView>? Photos, string? Error)> UpdateAsync(string userId, List<GalleryPhoto> photos);
    Task<bool> RemoveAsync(string userId, string photoId);
}

public class GalleryService : IGalleryService
{
    public const string Container = "host-gallery";
    public const int Limit = 6; // ponytail: one limit for everyone; FestiveGuest Plus (payments phase) raises it
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

    private static string LimitMessage => $"You can add up to {Limit} photos. Remove one to add another.";

    public List<GalleryPhotoView> View(UserEntity user) =>
        Parse(user.Gallery).Select(p => new GalleryPhotoView(p.Id, p.Caption, _files.GenerateReadSasUrl(BlobName(user.RowKey, p.Id), Container))).ToList();

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

    public async Task<(string? PhotoId, string? UploadUrl, string? Error)> StartUploadAsync(string userId)
    {
        var (user, error) = await HostAsync(userId);
        if (user == null) return (null, null, error);
        if (Parse(user.Gallery).Count >= Limit) return (null, null, LimitMessage);
        var photoId = Guid.NewGuid().ToString("N");
        return (photoId, await _files.GenerateUploadUrlAsync(Container, BlobName(userId, photoId)), null);
    }

    public async Task<(List<GalleryPhotoView>? Photos, string? Error)> AddAsync(string userId, string photoId, string? caption)
    {
        if (!Guid.TryParseExact(photoId, "N", out _)) return (null, "That photo wasn't found.");
        var (user, error) = await HostAsync(userId);
        if (user == null) return (null, error);

        var photos = Parse(user.Gallery);
        if (photos.Any(p => p.Id == photoId)) return (View(user), null); // added already (a retried request)
        if (photos.Count >= Limit) return (null, LimitMessage);
        if (!await _files.BlobExistsAsync(Container, BlobName(userId, photoId)))
            return (null, "That photo didn't finish uploading. Please try again.");

        photos.Add(new GalleryPhoto(photoId, Clean(caption)));
        return (await SaveAsync(user, photos), null);
    }

    /// <summary>New order and captions. Must be the same photos — adding and removing have their own calls.</summary>
    public async Task<(List<GalleryPhotoView>? Photos, string? Error)> UpdateAsync(string userId, List<GalleryPhoto> photos)
    {
        var (user, error) = await HostAsync(userId);
        if (user == null) return (null, error);

        var current = Parse(user.Gallery).Select(p => p.Id).ToHashSet();
        var wanted = photos.Select(p => p.Id).ToList();
        if (wanted.Count != current.Count || wanted.Distinct().Count() != wanted.Count || !wanted.All(current.Contains))
            return (null, "Your photos changed in another window. Please reload and try again.");

        return (await SaveAsync(user, photos.Select(p => new GalleryPhoto(p.Id, Clean(p.Caption))).ToList()), null);
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
