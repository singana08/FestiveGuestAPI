using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

/// <summary>Short-lived links to a member's pictures, for lists that show many members (trips, saved hosts).</summary>
public static class MemberPhotos
{
    public static string? Profile(UserEntity? user, IFileUploadService files)
    {
        if (string.IsNullOrEmpty(user?.ProfileImageUrl)) return null;
        try { return files.GenerateReadSasUrl(Path.GetFileName(new Uri(user.ProfileImageUrl).LocalPath), "profile-images"); }
        catch { return null; }
    }

    /// <summary>A host's gallery cover (first photo), if they have one.</summary>
    public static string? Cover(UserEntity? user, IGalleryService gallery) =>
        user?.UserType == "Host" ? gallery.View(user).FirstOrDefault()?.Url : null;
}
