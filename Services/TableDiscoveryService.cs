using Azure.Data.Tables;

namespace FestiveGuestAPI.Services;

public class TableDiscoveryService
{
    private readonly TableServiceClient _tableServiceClient;

    public TableDiscoveryService(TableServiceClient tableServiceClient)
    {
        _tableServiceClient = tableServiceClient;
    }

    public async Task<List<string>> GetTableNamesAsync()
    {
        var tableNames = new List<string>();
        await foreach (var table in _tableServiceClient.QueryAsync())
        {
            tableNames.Add(table.Name);
        }
        return tableNames;
    }

    public async Task<List<Dictionary<string, object>>> GetTableDataAsync(string tableName, int maxRows = 100)
    {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        var entities = new List<Dictionary<string, object>>();
        
        await foreach (var entity in tableClient.QueryAsync<TableEntity>(maxPerPage: maxRows))
        {
            entities.Add(entity.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }
        
        return entities;
    }

    public async Task<Dictionary<string, string>> GetTableSchemaAsync(string tableName)
    {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        var schema = new Dictionary<string, string>();
        
        await foreach (var entity in tableClient.QueryAsync<TableEntity>(maxPerPage: 1))
        {
            foreach (var property in entity)
            {
                if (!schema.ContainsKey(property.Key))
                {
                    schema[property.Key] = property.Value?.GetType().Name ?? "Unknown";
                }
            }
            break;
        }
        
        return schema;
    }
}