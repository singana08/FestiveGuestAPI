namespace FestiveGuestAPI.Models;

/// <summary>What a post card shows about its author; filled in per feed request, never stored.</summary>
public class PosterInfo
{
    public List<string> Photos { get; set; } = new(); // up to 4 gallery photos (hosts), cover first
    public int PhotoCount { get; set; }
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public string? ResponseBadge { get; set; }
    public int Stays { get; set; } // confirmed stays and meet-ups that have ended
}
