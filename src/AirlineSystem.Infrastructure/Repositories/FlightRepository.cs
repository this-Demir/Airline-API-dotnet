using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Interfaces;
using AirlineSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AirlineSystem.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IFlightRepository"/>.
/// Extends <see cref="GenericRepository{Flight}"/> with flight-schedule queries
/// required by the search (FR-04), booking (FR-05), check-in (FR-06), and
/// CSV upload (FR-03) pipelines.
/// </summary>
public class FlightRepository : GenericRepository<Flight>, IFlightRepository
{
    /// <summary>
    /// Initialises the repository with the shared database context.
    /// </summary>
    /// <param name="context">The EF Core context scoped to the current request.</param>
    public FlightRepository(AirlineDbContext context) : base(context) { }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>_requires:</b> <paramref name="flightNumber"/> and <paramref name="departureDate"/>
    /// together form the composite business key of the flight.
    /// <b>_ensures:</b> Uses the composite index <c>IX_Flights_FlightNumber_DepartureDate</c>
    /// for O(log n) lookup. The date comparison uses <c>.Date</c> to ignore the time
    /// component so that <c>2025-06-15 00:00</c> and <c>2025-06-15 14:30</c> both
    /// match a flight scheduled for <c>2025-06-15</c>.
    /// </remarks>
    public async Task<Flight?> GetByFlightNumberAndDateAsync(string flightNumber, DateTime departureDate) =>
        await _context.Flights
            .Include(f => f.OriginAirport)
            .Include(f => f.DestinationAirport)
            .FirstOrDefaultAsync(f =>
                f.FlightNumber == flightNumber &&
                f.DepartureDate.Date == departureDate.Date);

    /// <inheritdoc/>
    /// <remarks>
    /// <b>_requires:</b> All parameters are non-null and <paramref name="pageNumber"/>
    /// is &gt;= 1.
    /// <b>_ensures:</b>
    /// <list type="bullet">
    ///   <item>Only flights where <c>AvailableCapacity &gt;= numberOfSeats</c> are
    ///   included, satisfying FR-04.03 (sold-out exclusion).</item>
    ///   <item>Results are ordered by <c>DepartureDate</c> ascending for a
    ///   predictable, deterministic page order.</item>
    ///   <item>Page size is fixed at <b>10</b> per FR-04.05. This is not a
    ///   configurable value.</item>
    ///   <item><c>TotalCount</c> reflects the full matching set before pagination
    ///   so the caller can compute <c>TotalPages</c>.</item>
    ///   <item>Both <c>OriginAirport</c> and <c>DestinationAirport</c> navigation
    ///   properties are eagerly loaded so airport codes are available to the
    ///   service layer without additional queries.</item>
    /// </list>
    /// </remarks>
    public async Task<(IEnumerable<Flight> Items, int TotalCount)> SearchFlightsAsync(
        string? originCode,
        string? destinationCode,
        DateTime departureFrom,
        DateTime departureTo,
        int numberOfSeats,
        int pageNumber)
    {
        const int pageSize = 10;

        var query = _context.Flights
            .Include(f => f.OriginAirport)
            .Include(f => f.DestinationAirport)
            .Where(f => f.DepartureDate >= departureFrom
                     && f.DepartureDate <= departureTo
                     && f.AvailableCapacity >= numberOfSeats);

        if (!string.IsNullOrWhiteSpace(originCode))
            query = query.Where(f => f.OriginAirport.Code.ToUpper() == originCode.ToUpper());

        if (!string.IsNullOrWhiteSpace(destinationCode))
            query = query.Where(f => f.DestinationAirport.Code.ToUpper() == destinationCode.ToUpper());

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(f => f.DepartureDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>_requires:</b> <paramref name="flightNumber"/> and <paramref name="departureDate"/>
    /// together form the composite key to check.
    /// <b>_ensures:</b> Issues a single <c>SELECT EXISTS</c> query — no entity is
    /// materialised. Used exclusively by the CSV bulk-upload pipeline to reject
    /// duplicate <c>(FlightNumber, DepartureDate)</c> rows before insertion (FR-03.02).
    /// </remarks>
    public async Task<bool> ExistsAsync(string flightNumber, DateTime departureDate) =>
        await _context.Flights
            .AnyAsync(f =>
                f.FlightNumber == flightNumber &&
                f.DepartureDate.Date == departureDate.Date);

    /// <inheritdoc/>
    public async Task<IEnumerable<Flight>> GetAllWithAirportsAsync() =>
        await _context.Flights
            .Include(f => f.OriginAirport)
            .Include(f => f.DestinationAirport)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<Flight?> GetByIdWithAirportsAsync(Guid id) =>
        await _context.Flights
            .Include(f => f.OriginAirport)
            .Include(f => f.DestinationAirport)
            .FirstOrDefaultAsync(f => f.Id == id);
}
