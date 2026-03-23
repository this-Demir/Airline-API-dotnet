using AirlineSystem.Application.DTOs.Flights;
using AirlineSystem.Application.DTOs.Tickets;

namespace AirlineSystem.Application.Interfaces;

public interface ITicketService
{
    Task<TicketResponseDto> BuyTicketAsync(BuyTicketRequestDto request, Guid userId);
    Task<PaginatedResultDto<PassengerDto>> GetPassengerListAsync(string flightNumber, DateTime date, int pageNumber);
}
