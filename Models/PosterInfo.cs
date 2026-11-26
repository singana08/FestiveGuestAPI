namespace FestiveGuestAPI.Models;

/// <summary>What a post card shows about its author; filled in per feed request, never stored.</summary>
public class PosterInfo
{
    public List<string> Photos { get; set; } = new(); // the host's photos for this listing's cities, then "All places" (up to 12), cover first
    public List<string?> PhotoCities { get; set; } = new(); // the city of each photo above (null = all places), so the page can put a searched city first
    public int PhotoCount { get; set; }
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public string? ResponseBadge { get; set; }
    public int Stays { get; set; } // confirmed stays and meet-ups that have ended
    public string? VerifiedHow { get; set; }     // "Verified by FestiveGuest": how the team checked them (null if not)
    public DateTime? VerifiedSince { get; set; }
}
