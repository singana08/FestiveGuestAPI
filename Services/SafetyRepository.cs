using Azure;
using Azure.Data.Tables;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

public interface ISafetyRepository
{
    /// <summary>True when either member has blocked the other.</summary>
    Task<bool> IsBlockedEitherWayAsync(string userA, string userB);
    /// <summary>Members hidden from this user: ones they blocked, and ones who blocked them.</summary>
    Task<HashSet<string>> GetHiddenUserIdsAsync(string userId);
    Task<List<UserBlockEntity>> GetBlocksAsync(string blockerId);
    Task BlockAsync(UserBlockEntity block);
    Task UnblockAsync(string blockerId, string blockedId);
    Task AddReportAsync(UserReportEntity report);
    /// <summary>All reports, newest first.</summary>
    Task<List<UserReportEntity>> GetReportsAsync();
    /// <summary>False when the report doesn't exist.</summary>
    Task<bool> SetReportStatusAsync(string reportId, string status);
}

public class SafetyRepository : ISafetyRepository
{
    private readonly TableClient _blocks;
    private readonly TableClient _reports;

    public SafetyRepository(TableServiceClient tableServiceClient)
    {
        _blocks = tableServiceClient.GetTableClient("UserBlocks");
        _blocks.CreateIfNotExists();
        _reports = tableServiceClient.GetTableClient("UserReports");
        _reports.CreateIfNotExists();
    }

    private async Task<bool> HasBlockedAsync(string blockerId, string blockedId) =>
        (await _blocks.GetEntityIfExistsAsync<UserBlockEntity>(blockerId, blockedId)).HasValue;

    public async Task<bool> IsBlockedEitherWayAsync(string userA, string userB) =>
        await HasBlockedAsync(userA, userB) || await HasBlockedAsync(userB, userA);

    public async Task<HashSet<string>> GetHiddenUserIdsAsync(string userId)
    {
        var hidden = new HashSet<string>();
        await foreach (var b in _blocks.QueryAsync<UserBlockEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {userId}")))
            hidden.Add(b.RowKey);
        // ponytail: cross-partition scan for "who blocked me"; add a reverse-index partition if this gets slow.
        await foreach (var b in _blocks.QueryAsync<UserBlockEntity>(TableClient.CreateQueryFilter($"RowKey eq {userId}")))
            hidden.Add(b.PartitionKey);
        return hidden;
    }

    public async Task<List<UserBlockEntity>> GetBlocksAsync(string blockerId)
    {
        var list = new List<UserBlockEntity>();
        await foreach (var b in _blocks.QueryAsync<UserBlockEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {blockerId}")))
            list.Add(b);
        return list.OrderByDescending(b => b.CreatedAt).ToList();
    }

    public Task BlockAsync(UserBlockEntity block) => _blocks.UpsertEntityAsync(block, TableUpdateMode.Replace);

    public async Task UnblockAsync(string blockerId, string blockedId)
    {
        try { await _blocks.DeleteEntityAsync(blockerId, blockedId); }
        catch (RequestFailedException ex) when (ex.Status == 404) { /* already unblocked */ }
    }

    public Task AddReportAsync(UserReportEntity report) => _reports.AddEntityAsync(report);

    public async Task<List<UserReportEntity>> GetReportsAsync()
    {
        var list = new List<UserReportEntity>();
        await foreach (var r in _reports.QueryAsync<UserReportEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {"Report"}")))
            list.Add(r);
        return list.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public async Task<bool> SetReportStatusAsync(string reportId, string status)
    {
        var found = await _reports.GetEntityIfExistsAsync<UserReportEntity>("Report", reportId);
        if (!found.HasValue || found.Value == null) return false;
        found.Value.Status = status;
        await _reports.UpdateEntityAsync(found.Value, found.Value.ETag, TableUpdateMode.Replace);
        return true;
    }
}
