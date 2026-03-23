namespace AirlineSystem.Application.DTOs.Flights;

public class PassengerDto
{
    public string FullName { get; set; } = string.Empty;
    public int? SeatNumber { get; set; }
    public bool IsCheckedIn { get; set; }
}
