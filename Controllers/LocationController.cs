using Microsoft.AspNetCore.Mvc;
using FestiveGuestAPI.Services;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationController : ControllerBase
{
    private readonly ILocationRepository _locationRepository;

    public LocationController(ILocationRepository locationRepository)
    {
        _locationRepository = locationRepository;
    }

    [HttpGet("states-with-cities")]
    public async Task<IActionResult> GetStatesWithCities()
    {
        var locations = await _locationRepository.GetAllLocationsAsync();
        
        var result = locations
            .Where(l => !string.IsNullOrEmpty(l.PartitionKey) && !string.IsNullOrEmpty(l.RowKey))
            .GroupBy(l => l.PartitionKey)
            .ToDictionary(
                g => g.Key,
                g => g.Select(l => l.RowKey).Distinct().OrderBy(c => c).ToArray()
            );

        return Ok(result);
    }

    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok(new { message = "API is working", timestamp = DateTime.Now });
    }
}