using AirlineSystem.Application.DTOs.Flights;
using AirlineSystem.Application.DTOs.Tickets;

namespace AirlineSystem.Application.Interfaces;

/// <summary>
/// Defines ticket purchase (FR-05) and admin passenger-manifest retrieval (FR-07) operations.
/// </summary>
public interface ITicketService
{
    /// <summary>
    /// Atomically purchases tickets for one or more passengers on a specific flight.
    /// </summary>
    /// <param name="request">
    /// Booking payload: flight number, departure date, and the list of passenger full names.
    /// </param>
    /// <param name="userId">
    /// The GUID of the authenticated <c>Customer</c> performing the purchase,
    /// extracted from the JWT claims by the API layer.
    /// </param>
    /// <returns>
    /// A <see cref="TicketResponseDto"/> with <c>Status = "Confirmed"</c> and the
    /// generated PNR code on success, or <c>Status = "SoldOut"</c> (with no PNR)
    /// when available capacity is insufficient.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no flight exists for the given number and departure date.
    /// </exception>
    Task<TicketResponseDto> BuyTicketAsync(BuyTicketRequestDto request, Guid userId);

    /// <summary>
    /// Retrieves the paginated passenger manifest for a specific flight (admin use, FR-07).
    /// </summary>
    /// <param name="flightNumber">The flight number to query.</param>
    /// <param name="date">The departure date of the flight (UTC).</param>
    /// <param name="pageNumber">1-based page number. Page size is fixed at 10.</param>
    /// <returns>
    /// A paginated result containing <see cref="PassengerDto"/> records for all
    /// passengers booked on the flight, regardless of check-in status.
    /// </returns>
    Task<PaginatedResultDto<PassengerDto>> GetPassengerListAsync(string flightNumber, DateTime date, int pageNumber);
}
