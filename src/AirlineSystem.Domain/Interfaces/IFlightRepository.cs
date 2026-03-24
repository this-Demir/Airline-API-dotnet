using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Domain.Interfaces;

/// <summary>
/// Extends <see cref="IGenericRepository{Flight}"/> with flight-schedule queries
/// required by the search, booking, and check-in pipelines.
/// </summary>
public interface IFlightRepository : IGenericRepository<Flight>
{
    /// <summary>
    /// Retrieves a specific flight by its composite business key of flight number and departure date.
    /// </summary>
    /// <remarks>
    /// Uses the composite index on <c>(FlightNumber, DepartureDate)</c> for efficient lookup.
    /// This method is used by the Buy Ticket and Check-in pipelines to locate the exact flight.
    /// </remarks>
    /// <param name="flightNumber">The airline flight number (e.g., <c>"TK1923"</c>).</param>
    /// <param name="departureDate">The scheduled departure date and time (UTC).</param>
    /// <returns>
    /// The matching <see cref="Flight"/> entity, or <c>null</c> if no flight exists
    /// for the given number and departure date.
    /// </returns>
    Task<Flight?> GetByFlightNumberAndDateAsync(string flightNumber, DateTime departureDate);

    /// <summary>
    /// Searches for available flights matching the given route and date range,
    /// filtered by seat availability, and returns a paginated result.
    /// </summary>
    /// <remarks>
    /// Implements the core filtering logic for FR-04:
    /// <list type="bullet">
    ///   <item>Excludes flights where <c>AvailableCapacity &lt; numberOfSeats</c>.</item>
    ///   <item>Page size is fixed at 10 (FR-04.05).</item>
    ///   <item>Airport codes are resolved from the <see cref="Flight"/>'s navigation properties.</item>
    /// </list>
    /// The implementation must eagerly load <c>OriginAirport</c> and <c>DestinationAirport</c>
    /// navigation properties so that airport codes are available to the service layer.
    /// </remarks>
    /// <param name="originCode">IATA code of the departure airport.</param>
    /// <param name="destinationCode">IATA code of the arrival airport.</param>
    /// <param name="departureFrom">Earliest acceptable departure date/time (UTC, inclusive).</param>
    /// <param name="departureTo">Latest acceptable departure date/time (UTC, inclusive).</param>
    /// <param name="numberOfSeats">Minimum seats required; flights with fewer available seats are excluded.</param>
    /// <param name="pageNumber">1-based page number for the paginated result set.</param>
    /// <returns>
    /// A tuple of the page's <see cref="Flight"/> items and the total count of matching flights
    /// (before pagination), used to compute <c>TotalPages</c>.
    /// </returns>
    Task<(IEnumerable<Flight> Items, int TotalCount)> SearchFlightsAsync(
        string? originCode,
        string? destinationCode,
        DateTime departureFrom,
        DateTime departureTo,
        int numberOfSeats,
        int pageNumber);

    /// <summary>
    /// Checks whether a flight with the given composite key already exists in the store.
    /// </summary>
    /// <remarks>
    /// Used exclusively by the CSV bulk-upload pipeline to prevent inserting duplicate
    /// <c>(FlightNumber, DepartureDate)</c> records (FR-03.02).
    /// </remarks>
    /// <param name="flightNumber">The flight number to check.</param>
    /// <param name="departureDate">The departure date to check.</param>
    /// <returns>
    /// <c>true</c> if a record with this composite key exists; <c>false</c> otherwise.
    /// </returns>
    Task<bool> ExistsAsync(string flightNumber, DateTime departureDate);

    /// <summary>
    /// Retrieves all flights with their <see cref="Airport"/> navigation properties eagerly loaded.
    /// </summary>
    /// <returns>All <see cref="Flight"/> entities with <c>OriginAirport</c> and <c>DestinationAirport</c> populated.</returns>
    Task<IEnumerable<Flight>> GetAllWithAirportsAsync();

    /// <summary>
    /// Retrieves a single flight by its primary key with <see cref="Airport"/> navigation properties eagerly loaded.
    /// </summary>
    /// <param name="id">The flight's primary key.</param>
    /// <returns>The matching <see cref="Flight"/> with airports populated, or <c>null</c> if not found.</returns>
    Task<Flight?> GetByIdWithAirportsAsync(Guid id);
}
