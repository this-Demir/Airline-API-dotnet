namespace AirlineSystem.Application.DTOs.CheckIn;

public class CheckInRequestDto
{
    public string PnrCode { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
}
