using Azure;
using Azure.Data.Tables;
using FestiveGuestAPI.Configuration;
using FestiveGuestAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FestiveGuestAPI.Controllers;

/// <summary>A sponsored card on the Posts page. One partition; RowKey = id. Cities empty = every city.</summary>
public class SponsoredEntity : ITableEntity
{
    public string PartitionKey { get; set; } = SponsoredController.Partition;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string ImageId { get; set; } = string.Empty;
    public string Cities { get; set; } = string.Empty; // comma-separated
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool Paused { get; set; }
    public int Views { get; set; }
    public int Clicks { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SponsoredRequest
{
    public string? Title { get; set; }
    public string? Text { get; set; }
    public string? Link { get; set; }
    public string? ImageId { get; set; }
    public List<string>? Cities { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool Paused { get; set; }
}

/// <summary>
/// Sponsored cards: admins add them; members see the ones running for their cities. Counts are totals
/// per card (views, clicks) — nothing is stored about who saw or clicked.
/// </summary>
[ApiController]
[Route("api/sponsored")]
[Authorize]
public class SponsoredController : ControllerBase
{
    public const string Partition = "SPONSORED";
    public const string Container = "sponsored";
    public const int Shown = 3, TitleMax = 60, TextMax = 160, CitiesMax = 30;

    private readonly TableServiceClient _tables;
    private readonly IFileUploadService _files;

    public SponsoredController(TableServiceClient tables, IFileUploadService files)
    {
        _tables = tables;
        _files = files;
    }

    private TableClient Table => _tables.GetTableClient("Sponsored");
    private static DateTime Today => DateTime.UtcNow.Date;
    private static string BlobName(string imageId) => $"{imageId}.jpg";

    public static List<string> CityList(string? cities) =>
        (cities ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>Running today, not paused, and for one of these cities (or for every city).</summary>
    public static bool IsShowing(SponsoredEntity s, DateTime today, ICollection<string> cities)
    {
        if (s.Paused || s.StartDate.Date > today || s.EndDate.Date < today) return false;
        var mine = CityList(s.Cities);
        return mine.Count == 0 || mine.Any(c => cities.Contains(c, StringComparer.OrdinalIgnoreCase));
    }

    private async Task<List<SponsoredEntity>> AllAsync()
    {
        var rows = new List<SponsoredEntity>();
        try { await foreach (var r in Table.QueryAsync<SponsoredEntity>(r => r.PartitionKey == Partition)) rows.Add(r); }
        catch (RequestFailedException ex) when (ex.Status == 404) { }
        return rows;
    }

    private async Task<SponsoredEntity?> FindAsync(string id)
    {
        try { return (await Table.GetEntityAsync<SponsoredEntity>(Partition, id)).Value; }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }

    // ponytail: read-modify-write; two increments at the same instant lose one. Fine at this traffic.
    private async Task CountAsync(SponsoredEntity s, bool click)
    {
        if (click) s.Clicks++; else s.Views++;
        try { await Table.UpdateEntityAsync(s, s.ETag, TableUpdateMode.Merge); }
        catch (RequestFailedException ex) when (ex.Status == 412) { }
    }

    private object Card(SponsoredEntity s) => new
    {
        id = s.RowKey, title = s.Title, text = s.Text, link = s.Link,
        imageUrl = _files.GenerateReadSasUrl(BlobName(s.ImageId), Container),
    };

    /// <summary>Up to 3 cards running today for the cities the member is looking at (comma-separated). Each one returned counts as a view.</summary>
    [HttpGet]
    public async Task<IActionResult> ForMe([FromQuery] string? cities)
    {
        var wanted = CityList(cities);
        var showing = (await AllAsync()).Where(s => IsShowing(s, Today, wanted)).OrderBy(_ => Random.Shared.Next()).Take(Shown).ToList();
        await Task.WhenAll(showing.Select(s => CountAsync(s, click: false)));
        return Ok(showing.Select(Card));
    }

    [HttpPost("{id}/click")]
    public async Task<IActionResult> Click(string id)
    {
        if (await FindAsync(id) is { } s) await CountAsync(s, click: true);
        return NoContent();
    }

    // ── Admin ──

    [HttpGet("admin")]
    [AdminAuthorize]
    public async Task<IActionResult> List() => Ok((await AllAsync()).OrderByDescending(s => s.StartDate).Select(s => new
    {
        id = s.RowKey, title = s.Title, text = s.Text, link = s.Link, imageId = s.ImageId,
        imageUrl = _files.GenerateReadSasUrl(BlobName(s.ImageId), Container),
        cities = CityList(s.Cities), startDate = s.StartDate, endDate = s.EndDate, paused = s.Paused,
        views = s.Views, clicks = s.Clicks,
        state = s.Paused ? "Paused" : s.EndDate.Date < Today ? "Ended" : s.StartDate.Date > Today ? "Scheduled" : "Running",
    }));

    [HttpPost("admin/upload-url")]
    [AdminAuthorize]
    public async Task<IActionResult> UploadUrl()
    {
        var imageId = Guid.NewGuid().ToString("N");
        return Ok(new { imageId, uploadUrl = await _files.GenerateUploadUrlAsync(Container, BlobName(imageId)) });
    }

    private async Task<string?> ApplyAsync(SponsoredEntity s, SponsoredRequest r)
    {
        var title = (r.Title ?? "").Trim();
        var text = (r.Text ?? "").Trim();
        var link = (r.Link ?? "").Trim();
        var cities = (r.Cities ?? new()).Select(c => c.Trim()).Where(c => c.Length > 0 && !c.Contains(','))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (title.Length == 0 || title.Length > TitleMax) return $"Add a title (up to {TitleMax} characters).";
        if (text.Length > TextMax) return $"Keep the text to {TextMax} characters.";
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return "The link must be a full https:// address.";
        if (cities.Count > CitiesMax) return $"Pick up to {CitiesMax} cities, or none for every city.";
        if (r.StartDate is not { } start || r.EndDate is not { } end) return "Pick the start and end dates.";
        if (end.Date < start.Date) return "The end date must be on or after the start date.";
        if (r.ImageId != s.ImageId)
        {
            if (!Guid.TryParseExact(r.ImageId, "N", out _) || !await _files.BlobExistsAsync(Container, BlobName(r.ImageId!)))
                return "Add an image.";
            if (s.ImageId.Length > 0) await _files.DeleteBlobAsync(Container, BlobName(s.ImageId)); // replaced
            s.ImageId = r.ImageId!;
        }

        (s.Title, s.Text, s.Link, s.Cities, s.StartDate, s.EndDate, s.Paused) =
            (title, text, link, string.Join(",", cities), DateTime.SpecifyKind(start.Date, DateTimeKind.Utc), DateTime.SpecifyKind(end.Date, DateTimeKind.Utc), r.Paused);
        return null;
    }

    [HttpPost("admin")]
    [AdminAuthorize]
    public async Task<IActionResult> Create([FromBody] SponsoredRequest request)
    {
        var s = new SponsoredEntity { RowKey = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.UtcNow };
        if (await ApplyAsync(s, request) is { } error) return BadRequest(new { message = error });
        await Table.CreateIfNotExistsAsync();
        await Table.AddEntityAsync(s);
        return Ok(new { id = s.RowKey });
    }

    [HttpPut("admin/{id}")]
    [AdminAuthorize]
    public async Task<IActionResult> Update(string id, [FromBody] SponsoredRequest request)
    {
        if (await FindAsync(id) is not { } s) return NotFound(new { message = "That card wasn't found." });
        if (await ApplyAsync(s, request) is { } error) return BadRequest(new { message = error });
        await Table.UpdateEntityAsync(s, ETag.All, TableUpdateMode.Replace);
        return NoContent();
    }

    [HttpDelete("admin/{id}")]
    [AdminAuthorize]
    public async Task<IActionResult> Delete(string id)
    {
        if (await FindAsync(id) is not { } s) return NotFound(new { message = "That card wasn't found." });
        await Table.DeleteEntityAsync(Partition, id);
        if (s.ImageId.Length > 0) await _files.DeleteBlobAsync(Container, BlobName(s.ImageId));
        return NoContent();
    }
}
