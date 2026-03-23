using AirlineSystem.Application.DTOs.Flights;
using AirlineSystem.Application.DTOs.Tickets;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Application.Services;

public class TicketService : ITicketService
{
    private readonly IUnitOfWork _uow;

    public TicketService(IUnitOfWork uow) => _uow = uow;

    public async Task<TicketResponseDto> BuyTicketAsync(BuyTicketRequestDto request, Guid userId)
    {
        var flight = await _uow.Flights.GetByFlightNumberAndDateAsync(request.FlightNumber, request.Date)
            ?? throw new KeyNotFoundException($"Flight '{request.FlightNumber}' on {request.Date:yyyy-MM-dd} was not found.");

        if (flight.AvailableCapacity < request.PassengerNames.Count)
            return new TicketResponseDto { Status = "SoldOut" };

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            PnrCode = GeneratePnr(),
            UserId = userId,
            TransactionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var name in request.PassengerNames)
        {
            booking.Passengers.Add(new Passenger
            {
                Id = Guid.NewGuid(),
                FullName = name,
                IsCheckedIn = false,
                BookingId = booking.Id,
                FlightId = flight.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        flight.AvailableCapacity -= request.PassengerNames.Count;
        _uow.Flights.Update(flight);

        await _uow.Bookings.AddAsync(booking);
        await _uow.SaveChangesAsync();

        return new TicketResponseDto { Status = "Confirmed", PnrCode = booking.PnrCode };
    }

    public async Task<PaginatedResultDto<PassengerDto>> GetPassengerListAsync(
        string flightNumber, DateTime date, int pageNumber)
    {
        var (passengers, totalCount) = await _uow.Passengers.GetFlightManifestAsync(
            flightNumber, date, pageNumber);

        var items = passengers.Select(p => new PassengerDto
        {
            FullName = p.FullName,
            SeatNumber = p.SeatNumber,
            IsCheckedIn = p.IsCheckedIn
        });

        return PaginatedResultDto<PassengerDto>.Create(items, totalCount, pageNumber);
    }

    private static string GeneratePnr() =>
        Guid.NewGuid().ToString("N")[..6].ToUpper();
}
