using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Interfaces;
using AirlineSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AirlineSystem.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPassengerRepository"/>.
/// Extends <see cref="GenericRepository{Passenger}"/> with check-in validation
/// and manifest queries required by FR-06 and FR-07.
/// </summary>
public class PassengerRepository : GenericRepository<Passenger>, IPassengerRepository
{
    /// <summary>
    /// Initialises the repository with the shared database context.
    /// </summary>
    /// <param name="context">The EF Core context scoped to the current request.</param>
    public PassengerRepository(AirlineDbContext context) : base(context) { }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>_requires:</b>
    /// <list type="bullet">
    ///   <item><paramref name="flightNumber"/> is the airline flight code (e.g., <c>"TK1923"</c>).</item>
    ///   <item><paramref name="departureDate"/> is the scheduled departure in UTC.</item>
    ///   <item><paramref name="fullName"/> is the name as recorded at booking time.</item>
    /// </list>
    /// <b>_ensures:</b>
    /// <list type="bullet">
    ///   <item>The <see cref="Passenger.Flight"/> navigation property is eagerly loaded
    ///   so the caller can access <c>passenger.FlightId</c> without an extra query.</item>
    ///   <item>The <paramref name="fullName"/> comparison is case-insensitive.</item>
    ///   <item>The date comparison uses <c>.Date</c> to ignore the time component,
    ///   matching any departure time on the given calendar day.</item>
    ///   <item>Returns <c>null</c> — not an exception — when no match is found,
    ///   so <c>CheckInService</c> can return a <c>Status = "Failed"</c> response
    ///   rather than propagating an error (FR-06.03).</item>
    /// </list>
    /// </remarks>
    public async Task<Passenger?> FindForCheckinAsync(
        string flightNumber, DateTime departureDate, string fullName) =>
        await _context.Passengers
            .Include(p => p.Flight)
            .FirstOrDefaultAsync(p =>
                p.Flight.FlightNumber == flightNumber &&
                p.Flight.DepartureDate.Date == departureDate.Date &&
                p.FullName.ToLower() == fullName.ToLower());

    /// <inheritdoc/>
    /// <remarks>
    /// <b>_requires:</b> <paramref name="flightId"/> is the internal GUID of an
    /// existing <see cref="Flight"/>.
    /// <b>_ensures:</b>
    /// <list type="bullet">
    ///   <item>Only passengers where <c>IsCheckedIn == true</c> and
    ///   <c>SeatNumber != null</c> are considered, preventing unchecked-in
    ///   passengers from influencing the sequence.</item>
    ///   <item>Returns <c>1</c> when no passengers on this flight have been
    ///   checked in yet (<c>MAX</c> of an empty set is <c>null</c>, coalesced to 0).</item>
    ///   <item>The result is <c>MAX(SeatNumber) + 1</c>, satisfying the sequential
    ///   numbering rule in FR-06.02.</item>
    /// </list>
    /// </remarks>
    public async Task<int> GetNextSeatNumberAsync(Guid flightId)
    {
        var maxSeat = await _context.Passengers
            .Where(p => p.FlightId == flightId && p.IsCheckedIn && p.SeatNumber != null)
            .MaxAsync(p => (int?)p.SeatNumber) ?? 0;

        return maxSeat + 1;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>_requires:</b> <paramref name="pageNumber"/> is &gt;= 1.
    /// <b>_ensures:</b>
    /// <list type="bullet">
    ///   <item>Results include all passengers on the flight regardless of check-in
    ///   status (FR-07.02).</item>
    ///   <item>Results are ordered alphabetically by <c>FullName</c> for a consistent,
    ///   deterministic page order.</item>
    ///   <item>Page size is fixed at <b>10</b> per FR-07.04.</item>
    ///   <item><c>TotalCount</c> is the full pre-pagination count so the API can
    ///   return <c>TotalPages</c> to the caller.</item>
    /// </list>
    /// </remarks>
    public async Task<(IEnumerable<Passenger> Items, int TotalCount)> GetFlightManifestAsync(
        string flightNumber, DateTime departureDate, int pageNumber)
    {
        const int pageSize = 10;

        var query = _context.Passengers
            .Include(p => p.Flight)
            .Where(p =>
                p.Flight.FlightNumber == flightNumber &&
                p.Flight.DepartureDate.Date == departureDate.Date)
            .OrderBy(p => p.FullName);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
