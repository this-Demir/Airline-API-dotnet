namespace AirlineSystem.Application.DTOs.Flights;

public class FlightDto
{
    public Guid Id { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public DateTime DepartureDate { get; set; }
    public DateTime ArrivalDate { get; set; }
    public int DurationMinutes { get; set; }
    public int TotalCapacity { get; set; }
    public int AvailableCapacity { get; set; }
    public string OriginAirportCode { get; set; } = string.Empty;
    public string DestinationAirportCode { get; set; } = string.Empty;
}
