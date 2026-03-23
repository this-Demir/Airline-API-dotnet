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
    ///   <item>A <c>Passenger</c> record linked to a <c>Booking</c> must exist for the
    ///   specified flight, date, and passenger name — confirming a ticket was purchased.</item>
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
    ///   <item>A passenger without a ticket (no matching <c>Passenger</c> record) receives
    ///   <c>Status = "Failed"</c> — not an exception — to allow the API layer to return HTTP 200
    ///   with a descriptive status body (FR-06.03).</item>
    ///   <item>Re-checking an already checked-in passenger is silently rejected with
    ///   <c>Status = "Failed"</c> to prevent duplicate seat assignment.</item>
    ///   <item>Seat numbering is sequential per flight, not per cabin class or booking order.</item>
    /// </list>
    /// </remarks>
    public async Task<CheckInResponseDto> CheckInPassengerAsync(CheckInRequestDto request)
    {
        var passenger = await _uow.Passengers.FindForCheckinAsync(
            request.FlightNumber, request.Date, request.PassengerName);

        if (passenger is null)
            return new CheckInResponseDto
            {
                Status = "Failed",
                Message = "No ticket found for this passenger on this flight."
            };

        if (passenger.IsCheckedIn)
            return new CheckInResponseDto
            {
                Status = "Failed",
                Message = "Passenger has already checked in."
            };

        var nextSeat = await _uow.Passengers.GetNextSeatNumberAsync(passenger.FlightId);
        passenger.SeatNumber = nextSeat;
        passenger.IsCheckedIn = true;

        _uow.Passengers.Update(passenger);
        await _uow.SaveChangesAsync();

        return new CheckInResponseDto
        {
            Status = "Success",
            SeatNumber = nextSeat,
            FullName = passenger.FullName
        };
    }
}
