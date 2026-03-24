using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Domain.Interfaces;

/// <summary>
/// Extends <see cref="IGenericRepository{Passenger}"/> with check-in and manifest
/// query operations.
/// </summary>
public interface IPassengerRepository : IGenericRepository<Passenger>
{
    /// <summary>
    /// Calculates the next sequential seat number to assign during check-in.
    /// </summary>
    /// <remarks>
    /// Implements the sequential seat assignment rule from FR-06.02:
    /// <c>nextSeat = MAX(SeatNumber) + 1</c> across all checked-in passengers on the given flight.
    /// Returns <c>1</c> when no seats have been assigned yet (i.e., the flight has no checked-in passengers).
    /// </remarks>
    /// <param name="flightId">The internal GUID of the flight.</param>
    /// <returns>
    /// An integer representing the next available seat number, starting at <c>1</c>.
    /// </returns>
    Task<int> GetNextSeatNumberAsync(Guid flightId);

    /// <summary>
    /// Retrieves the paginated passenger manifest for a specific flight schedule,
    /// for use by administrators (FR-07).
    /// </summary>
    /// <remarks>
    /// Page size is fixed at 10 (FR-07.04). Each item in the result represents one
    /// passenger booked on that flight, regardless of check-in status.
    /// </remarks>
    /// <param name="flightNumber">The flight number to query.</param>
    /// <param name="departureDate">The departure date of the flight (UTC).</param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <returns>
    /// A tuple containing the page's <see cref="Passenger"/> items and the total count
    /// of passengers on this flight (before pagination).
    /// </returns>
    Task<(IEnumerable<Passenger> Items, int TotalCount)> GetFlightManifestAsync(
        string flightNumber,
        DateTime departureDate,
        int pageNumber);
}
