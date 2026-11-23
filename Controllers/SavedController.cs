using System.Security.Claims;
using Azure;
using Azure.Data.Tables;
using FestiveGuestAPI.Models;
using FestiveGuestAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FestiveGuestAPI.Controllers;

/// <summary>A member's saved (favourite) hosts. Row per save: PartitionKey = member, RowKey = saved host.</summary>
public class SavedMemberEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public DateTime SavedAt { get; set; }
}

[ApiController]
[Route("api/saved")]
[Authorize]
public class SavedController : ControllerBase
{
    public const int Limit = 200;
    private readonly TableServiceClient _tables;
    private readonly IUserRepository _users;

    public SavedController(TableServiceClient tables, IUserRepository users)
    {
        _tables = tables;
        _users = users;
    }

    private string? Me => User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    private TableClient Saved => _tables.GetTableClient("SavedMembers");

    private async Task<List<SavedMemberEntity>> RowsAsync(string me)
    {
        var rows = new List<SavedMemberEntity>();
        try { await foreach (var r in Saved.QueryAsync<SavedMemberEntity>(r => r.PartitionKey == me)) rows.Add(r); }
        catch (RequestFailedException ex) when (ex.Status == 404) { }
        return rows;
    }

    /// <summary>Just the ids, for the ♡ on cards and profiles.</summary>
    [HttpGet("ids")]
    public async Task<IActionResult> Ids() => Me is { } me ? Ok((await RowsAsync(me)).Select(r => r.RowKey)) : Unauthorized();

    /// <summary>The Saved page: each host with photo, city, rating, confirmed stays and how many posts they have up.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromServices] IGalleryService gallery, [FromServices] IFileUploadService files,
        [FromServices] IReviewService reviews, [FromServices] IStayService stays, [FromServices] IHostPostRepository hostPosts)
    {
        if (Me is not { } me) return Unauthorized();
        var ratings = await reviews.SummariesAsync();
        var stayCounts = await stays.CompletedCountsAsync();
        var hosts = new List<object>();
        foreach (var row in (await RowsAsync(me)).OrderByDescending(r => r.SavedAt))
        {
            var host = await _users.GetUserByIdAsync(row.RowKey);
            if (host == null || host.Status != "Active") continue; // left, or suspended: quietly not shown
            ratings.TryGetValue(host.RowKey, out var rating);
            hosts.Add(new
            {
                id = host.RowKey, name = host.Name, location = host.Location, isVerified = host.IsVerified,
                photoUrl = MemberPhotos.Profile(host, files), coverUrl = MemberPhotos.Cover(host, gallery),
                rating = rating.Average, reviewCount = rating.Count, stays = stayCounts.GetValueOrDefault(host.RowKey),
                activePosts = (await hostPosts.GetByUserIdAsync(host.RowKey)).Count(p => p.Status == "Active"),
                savedAt = row.SavedAt,
            });
        }
        return Ok(hosts);
    }

    [HttpPut("{userId}")]
    public async Task<IActionResult> Save(string userId)
    {
        if (Me is not { } me) return Unauthorized();
        if (userId == me) return BadRequest(new { message = "You can't save yourself." });
        var host = await _users.GetUserByIdAsync(userId);
        if (host == null || host.Status != "Active" || host.UserType != "Host") return BadRequest(new { message = "Only hosts can be saved." });
        var rows = await RowsAsync(me);
        if (rows.Any(r => r.RowKey == userId)) return NoContent();
        if (rows.Count >= Limit) return BadRequest(new { message = $"You can save up to {Limit} hosts." });
        await Saved.CreateIfNotExistsAsync();
        await Saved.UpsertEntityAsync(new SavedMemberEntity { PartitionKey = me, RowKey = userId, SavedAt = DateTime.UtcNow });
        return NoContent();
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> Unsave(string userId)
    {
        if (Me is not { } me) return Unauthorized();
        try { await Saved.DeleteEntityAsync(me, userId, ETag.All); }
        catch (RequestFailedException ex) when (ex.Status == 404) { }
        return NoContent();
    }
}
