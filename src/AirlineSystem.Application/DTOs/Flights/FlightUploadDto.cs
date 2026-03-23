namespace AirlineSystem.Application.DTOs.Flights;

public class FlightUploadDto
{
    public string FlightNumber { get; set; } = string.Empty;
    public DateTime DepartureDate { get; set; }
    public DateTime ArrivalDate { get; set; }
    public int DurationMinutes { get; set; }
    public string OriginAirportCode { get; set; } = string.Empty;
    public string DestinationAirportCode { get; set; } = string.Empty;
    public int TotalCapacity { get; set; }
}
