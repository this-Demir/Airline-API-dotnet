using AirlineSystem.Application.DTOs.Flights;

namespace AirlineSystem.Application.Interfaces;

/// <summary>
/// Defines flight inventory management (FR-02, FR-03) and public flight search (FR-04) operations.
/// Admin-scoped operations are gated at the API layer via <c>[Authorize(Roles = "Admin")]</c>.
/// </summary>
public interface IFlightService
{
    /// <summary>
    /// Searches for available flights matching a route, date range, and seat requirement.
    /// All parameters are optional; sensible defaults are applied when omitted.
    /// </summary>
    /// <param name="request">
    /// Search parameters bound from the query string. Date fields (<c>DepartureFrom</c>,
    /// <c>DepartureTo</c>) must use <c>yyyy-MM-dd</c> format when supplied. Omitting them
    /// defaults the window to <em>today → today + 6 months</em> (UTC). Omitting
    /// <c>OriginCode</c> or <c>DestinationCode</c> disables that filter. <c>NumberOfPeople</c>
    /// defaults to 1; <c>IsRoundTrip</c> defaults to <c>false</c>.
    /// </param>
    /// <returns>
    /// A paginated result (page size fixed at 10) containing only flights with
    /// <c>AvailableCapacity &gt;= NumberOfPeople</c> (FR-04.03).
    /// When <c>IsRoundTrip</c> is <c>true</c>, <c>ReturnFlights</c> is also populated.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when a supplied date string does not conform to <c>yyyy-MM-dd</c>.
    /// Mapped to HTTP 400 by <c>ExceptionHandlingMiddleware</c>.
    /// </exception>
    Task<FlightSearchResponseDto> SearchFlightsAsync(FlightSearchRequestDto request);

    /// <summary>
    /// Bulk-imports flights from a CSV stream, applying per-row validation rules (FR-03).
    /// </summary>
    /// <remarks>
    /// Partial success is supported: rows that pass all validations are persisted
    /// while invalid rows accumulate descriptive error messages. A single
    /// <c>SaveChangesAsync</c> call commits all valid rows atomically.
    /// </remarks>
    /// <param name="csvStream">
    /// A readable UTF-8 CSV stream. Column headers must match the property names
    /// of <see cref="FlightUploadDto"/> (case-insensitive).
    /// </param>
    /// <returns>
    /// A tuple of <c>SuccessCount</c> (rows inserted) and <c>Errors</c>
    /// (per-row failure descriptions for any rejected records).
    /// </returns>
    Task<(int SuccessCount, List<string> Errors)> UploadFlightsFromCsvAsync(Stream csvStream);

    /// <summary>Returns all flights in the system (admin use).</summary>
    /// <returns>An unordered collection of all <see cref="FlightDto"/> records.</returns>
    Task<IEnumerable<FlightDto>> GetAllAsync();

    /// <summary>Retrieves a single flight by its internal GUID.</summary>
    /// <param name="id">The flight's primary key.</param>
    /// <returns>The <see cref="FlightDto"/>, or <c>null</c> if not found.</returns>
    Task<FlightDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// Creates a single flight record manually (admin use).
    /// </summary>
    /// <param name="dto">Flight data including airport codes and schedule.</param>
    /// <returns>The newly created <see cref="FlightDto"/> with its generated <c>Id</c>.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the origin or destination airport code does not resolve to a known airport.
    /// </exception>
    Task<FlightDto> CreateAsync(FlightUploadDto dto);

    /// <summary>Updates mutable fields of an existing flight (admin use).</summary>
    /// <param name="id">The internal GUID of the flight to update.</param>
    /// <param name="dto">Replacement flight data.</param>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the flight or either airport code is not found.
    /// </exception>
    Task UpdateAsync(Guid id, FlightUploadDto dto);

    /// <summary>Permanently deletes a flight record (admin use).</summary>
    /// <param name="id">The internal GUID of the flight to delete.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the flight is not found.</exception>
    Task DeleteAsync(Guid id);
}
