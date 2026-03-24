namespace AirlineSystem.Application.DTOs.Flights;

public class FlightSearchRequestDto
{
    public string? OriginCode { get; set; }
    public string? DestinationCode { get; set; }
    public DateTime DepartureFrom { get; set; }
    public DateTime DepartureTo { get; set; }
    public int NumberOfPeople { get; set; } = 1;
    public bool IsRoundTrip { get; set; }
    public int PageNumber { get; set; } = 1;
}
