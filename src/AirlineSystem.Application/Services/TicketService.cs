using AirlineSystem.Application.DTOs.Flights;
using AirlineSystem.Application.DTOs.Tickets;
using AirlineSystem.Application.Exceptions;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Application.Services;

/// <summary>
/// Implements ticket purchasing (FR-05) and admin passenger-manifest retrieval (FR-07).
/// </summary>
public class TicketService : ITicketService
{
    private readonly IUnitOfWork _uow;

    public TicketService(IUnitOfWork uow) => _uow = uow;

    /// <inheritdoc/>
    /// <remarks>
    /// <b>PRE-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>Caller must be an authenticated <c>Customer</c>; <paramref name="userId"/>
    ///   is extracted from a valid JWT by the API layer (FR-05.05).</item>
    ///   <item>The flight identified by <c>FlightNumber + Date</c> must exist.</item>
    ///   <item><c>PassengerNames</c> must contain at least one name.</item>
    /// </list>
    /// <b>POST-CONDITIONS (on success):</b>
    /// <list type="bullet">
    ///   <item>A single <c>Booking</c> entity is created, grouping all passengers under
    ///   one PNR code.</item>
    ///   <item>Each <c>Passenger</c> entity has BOTH <c>BookingId</c> AND <c>FlightId</c>
    ///   explicitly populated (intentional denormalization for fast manifest queries).</item>
    ///   <item><c>Flight.AvailableCapacity</c> is decremented by <c>PassengerNames.Count</c>.</item>
    ///   <item>All mutations are committed atomically in a single <c>SaveChangesAsync</c> call.</item>
    /// </list>
    /// <b>BUSINESS RULES:</b>
    /// <list type="bullet">
    ///   <item>If <c>AvailableCapacity &lt; PassengerNames.Count</c>, returns
    ///   <c>Status = "SoldOut"</c> with no PNR and <b>no state mutation</b> (FR-05.04).</item>
    ///   <item>PNR codes are generated as the first 6 uppercase characters of a new GUID
    ///   in <c>"N"</c> format — probabilistically unique, not cryptographically guaranteed.</item>
    ///   <item>Round-trip bookings require two separate calls to this method, one per leg.</item>
    /// </list>
    /// </remarks>
    public async Task<TicketResponseDto> BuyTicketAsync(BuyTicketRequestDto request, Guid userId)
    {
        if (request.PassengerNames.Count == 0)
            throw new ArgumentException("At least one passenger name is required.", nameof(request));

        var flight = await _uow.Flights.GetByFlightNumberAndDateAsync(request.FlightNumber, request.FlightDate)
            ?? throw new KeyNotFoundException($"Flight '{request.FlightNumber}' on {request.FlightDate:yyyy-MM-dd} was not found.");

        if (flight.DepartureDate.ToUniversalTime() < DateTime.UtcNow)
            throw new InvalidOperationException(
                $"Cannot purchase tickets for flight '{request.FlightNumber}' — departure date has already passed.");

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

        await _uow.Bookings.AddAsync(booking);  // staged once; stays Added across retries

        const int MaxRetries = 3;
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            // attempt 0: capacity already verified above the loop.
            // attempt 1+: flight was reloaded with fresh AvailableCapacity — recheck.
            if (attempt > 0 && flight.AvailableCapacity < request.PassengerNames.Count)
                return new TicketResponseDto { Status = "SoldOut" };

            flight.AvailableCapacity -= request.PassengerNames.Count;
            _uow.Flights.Update(flight);  // re-marks Modified after each ReloadAsync reset

            try
            {
                await _uow.SaveChangesAsync();
                return new TicketResponseDto { Status = "Confirmed", PnrCode = booking.PnrCode };
            }
            catch (ConcurrencyConflictException)
            {
                // Refreshes AvailableCapacity and RowVersion in-place from the DB;
                // resets entity state to Unchanged so the next Update() is clean.
                await _uow.ReloadEntityAsync(flight);
            }
        }

        // S0 → [3 retries all lost to concurrency] → treat as sold out
        return new TicketResponseDto { Status = "SoldOut" };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>PRE-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>Caller must be an authenticated <c>Admin</c> (enforced at the API layer).</item>
    /// </list>
    /// <b>POST-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>No state is mutated; this is a read-only operation.</item>
    /// </list>
    /// </remarks>
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
