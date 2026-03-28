using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Interfaces;
using AirlineSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AirlineSystem.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAirportRepository"/>.
/// Extends <see cref="GenericRepository{Airport}"/> with IATA-code–based
/// lookup required by flight creation and CSV bulk upload (FR-03, FR-02).
/// </summary>
public class AirportRepository : GenericRepository<Airport>, IAirportRepository
{
    /// <summary>
    /// Initialises the repository with the shared database context.
    /// </summary>
    /// <param name="context">The EF Core context scoped to the current request.</param>
    public AirportRepository(AirlineDbContext context) : base(context) { }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>_requires:</b> <paramref name="code"/> is a non-null IATA airport code
    /// (e.g., <c>"IST"</c>, <c>"JFK"</c>).
    /// <b>_ensures:</b> Comparison is case-insensitive so that <c>"ist"</c> and
    /// <c>"IST"</c> resolve to the same record. Used by <c>FlightService</c> to
    /// resolve origin and destination airport codes during flight creation and
    /// CSV upload row validation.
    /// </remarks>
    public async Task<Airport?> GetByCodeAsync(string code) =>
        await _context.Airports
            .FirstOrDefaultAsync(a => a.Code.ToUpper() == code.ToUpper());

    /// <inheritdoc/>
    /// <remarks>
    /// Normalises all incoming codes to upper-case and issues a single
    /// <c>WHERE Code IN (...)</c> query against the Airports table.
    /// </remarks>
    public async Task<IEnumerable<Airport>> GetByCodesAsync(IEnumerable<string> codes)
    {
        var upper = codes.Select(c => c.ToUpper()).ToList();
        return await _context.Airports
            .Where(a => upper.Contains(a.Code))
            .ToListAsync();
    }
}
