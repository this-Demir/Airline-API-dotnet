using AirlineSystem.Application.DTOs.CheckIn;
using AirlineSystem.Application.Interfaces;

namespace AirlineSystem.Application.Services;

/// <summary>
/// Implements the passenger check-in operation (FR-06). This service is invoked
/// from a public endpoint and requires no authentication.
/// </summary>
public class CheckInService : ICheckInService
{
    private readonly IUnitOfWork _uow;

    public CheckInService(IUnitOfWork uow) => _uow = uow;

    /// <inheritdoc/>
    /// <remarks>
    /// <b>PRE-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>No authentication is required (FR-06.04).</item>
    ///   <item>A <c>Booking</c> with the given <c>PnrCode</c> must exist.</item>
    ///   <item>A <c>Passenger</c> with the given <c>PassengerName</c> (case-insensitive)
    ///   must be linked to that booking.</item>
    ///   <item>The passenger's <c>IsCheckedIn</c> flag must be <c>false</c>.</item>
    /// </list>
    /// <b>POST-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item><c>Passenger.SeatNumber</c> is set to <c>MAX(SeatNumber) + 1</c> across
    ///   all checked-in passengers on this flight (sequential numbering starting at 1).</item>
    ///   <item><c>Passenger.IsCheckedIn</c> is set to <c>true</c>.</item>
    ///   <item>Changes are persisted atomically via <c>SaveChangesAsync</c>.</item>
    /// </list>
    /// <b>BUSINESS RULES:</b>
    /// <list type="bullet">
    ///   <item>An unknown PNR or name mismatch returns <c>Status = "Failed"</c> (not an
    ///   exception) so the API layer always returns HTTP 200 with a descriptive body (FR-06.03).</item>
    ///   <item>Re-checking an already checked-in passenger is rejected with
    ///   <c>Status = "Failed"</c> to prevent duplicate seat assignment.</item>
    ///   <item>Seat numbering is sequential per flight, not per cabin class or booking order.</item>
    /// </list>
    /// </remarks>
    public async Task<CheckInResponseDto> CheckInPassengerAsync(CheckInRequestDto request)
    {
        var booking = await _uow.Bookings.GetByPnrAsync(request.PnrCode);
        if (booking is null)
            return new CheckInResponseDto
            {
                Status  = "Failed",
                Message = "No booking found for this PNR code."
            };

        var passenger = booking.Passengers
            .FirstOrDefault(p => p.FullName.Equals(request.PassengerName, StringComparison.OrdinalIgnoreCase));

        if (passenger is null)
            return new CheckInResponseDto
            {
                Status  = "Failed",
                Message = "No ticket found for this passenger on this booking."
            };

        if (passenger.IsCheckedIn)
            return new CheckInResponseDto
            {
                Status  = "Failed",
                Message = "Passenger has already checked in."
            };

        var nextSeat = await _uow.Passengers.GetNextSeatNumberAsync(passenger.FlightId);
        passenger.SeatNumber = nextSeat;
        passenger.IsCheckedIn = true;

        _uow.Passengers.Update(passenger);
        await _uow.SaveChangesAsync();

        return new CheckInResponseDto
        {
            Status     = "Success",
            SeatNumber = nextSeat,
            FullName   = passenger.FullName
        };
    }
}
