namespace AirlineSystem.Application.DTOs.CheckIn;

public class CheckInResponseDto
{
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public int? SeatNumber { get; set; }
    public string? FullName { get; set; }
}
