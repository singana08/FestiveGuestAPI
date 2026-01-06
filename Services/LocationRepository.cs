using Azure.Data.Tables;
using FestiveGuestAPI.Models;

namespace FestiveGuestAPI.Services;

public class LocationRepository : ILocationRepository
{
    private readonly TableClient _tableClient;

    public LocationRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient("Locations");
    }

    public async Task<IEnumerable<LocationEntity>> GetAllLocationsAsync()
    {
        var locations = new List<LocationEntity>();
        await foreach (var location in _tableClient.QueryAsync<LocationEntity>())
        {
            locations.Add(location);
        }
        return locations.OrderBy(l => l.State).ThenBy(l => l.City);
    }

    public async Task<IEnumerable<LocationEntity>> GetCitiesByStateAsync(string state)
    {
        var cities = new List<LocationEntity>();
        await foreach (var location in _tableClient.QueryAsync<LocationEntity>(l => l.State == state))
        {
            cities.Add(location);
        }
        return cities.OrderBy(c => c.City);
    }

    public async Task<IEnumerable<string>> GetStatesAsync()
    {
        var states = new HashSet<string>();
        await foreach (var location in _tableClient.QueryAsync<LocationEntity>())
        {
            states.Add(location.State);
        }
        return states.OrderBy(s => s);
    }
}