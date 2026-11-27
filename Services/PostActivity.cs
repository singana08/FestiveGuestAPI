using Azure;
using Azure.Data.Tables;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

/// <summary>
/// The traveller's view of their own trip post: how many times hosts opened it, and which members messaged them
/// about it. Kept in its own table, so the post that other members read never carries it.
/// </summary>
public static class PostActivity
{
    public const string Trip = "trip";

    private static TableClient Table(TableServiceClient tables) => tables.GetTableClient("PostActivity");

    public static async Task<PostActivityEntity> GetAsync(TableServiceClient tables, string postId)
    {
        try { return (await Table(tables).GetEntityAsync<PostActivityEntity>(Trip, postId)).Value; }
        catch (RequestFailedException ex) when (ex.Status == 404) { return new PostActivityEntity { PartitionKey = Trip, RowKey = postId }; }
    }

    public static Task CountViewAsync(TableServiceClient tables, string postId) =>
        ChangeAsync(tables, postId, a => { a.Views++; return true; });

    public static Task AddContactAsync(TableServiceClient tables, string postId, string memberId) =>
        ChangeAsync(tables, postId, a =>
        {
            var ids = a.ContactIds();
            if (ids.Contains(memberId)) return false;
            a.ContactedBy = string.Join(",", ids.Append(memberId));
            return true;
        });

    // Read, change, write back. If someone else wrote in between (409 on add, 412 on update), redo it on the fresh row.
    private static async Task ChangeAsync(TableServiceClient tables, string postId, Func<PostActivityEntity, bool> change)
    {
        var table = Table(tables);
        await table.CreateIfNotExistsAsync();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var row = await GetAsync(tables, postId);
            if (!change(row)) return;
            try
            {
                if (row.ETag == default) await table.AddEntityAsync(row);
                else await table.UpdateEntityAsync(row, row.ETag, TableUpdateMode.Replace);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status is 409 or 412) { }
        }
    }
}
