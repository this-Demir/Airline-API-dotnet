namespace AirlineSystem.Application.DTOs.Tickets;

public class BuyTicketRequestDto
{
    public string FlightNumber { get; set; } = string.Empty;
    public DateTime FlightDate { get; set; }
    public List<string> PassengerNames { get; set; } = new();
}
