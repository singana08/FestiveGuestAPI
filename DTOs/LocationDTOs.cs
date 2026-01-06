namespace FestiveGuestAPI.DTOs;

public class LocationDto
{
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class StateDto
{
    public string State { get; set; } = string.Empty;
    public List<string> Cities { get; set; } = new();
}