using Microsoft.AspNetCore.Mvc;
using FestiveGuestAPI.Services;
using FestiveGuestAPI.Configuration;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.Models;

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

    [HttpPost("add")]
    [Authorize]
    [AdminAuthorize]
    public async Task<IActionResult> AddLocation([FromBody] AddLocationRequest request)
    {
        try
        {
            var location = new LocationEntity
            {
                PartitionKey = request.State,
                RowKey = request.City,
                State = request.State,
                City = request.City
            };

            await _locationRepository.AddLocationAsync(location);
            return Ok(new { success = true, message = "Location added successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("delete")]
    [Authorize]
    [AdminAuthorize]
    public async Task<IActionResult> DeleteLocation([FromQuery] string state, [FromQuery] string city)
    {
        try
        {
            await _locationRepository.DeleteLocationAsync(state, city);
            return Ok(new { success = true, message = "Location deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok(new { message = "API is working", timestamp = DateTime.Now });
    }
}

public class AddLocationRequest
{
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}