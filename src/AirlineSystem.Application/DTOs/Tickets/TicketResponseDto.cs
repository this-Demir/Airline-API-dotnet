namespace AirlineSystem.Application.DTOs.Tickets;

public class TicketResponseDto
{
    public string Status { get; set; } = string.Empty;
    public string? PnrCode { get; set; }
}
