using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Domain.Interfaces;

/// <summary>
/// Extends <see cref="IGenericRepository{Airport}"/> with IATA-code–based lookup.
/// </summary>
public interface IAirportRepository : IGenericRepository<Airport>
{
    /// <summary>
    /// Retrieves an airport by its IATA code.
    /// </summary>
    /// <remarks>
    /// Used during flight creation and CSV upload to resolve origin and destination codes
    /// (e.g., "IST", "JFK") into their corresponding <see cref="Airport"/> entities.
    /// The lookup must be case-insensitive.
    /// </remarks>
    /// <param name="code">The IATA airport code (e.g., <c>"IST"</c>, <c>"ADB"</c>).</param>
    /// <returns>
    /// The matching <see cref="Airport"/> entity, or <c>null</c> if no airport is registered
    /// with the given code.
    /// </returns>
    Task<Airport?> GetByCodeAsync(string code);

    /// <summary>
    /// Returns all airports whose IATA codes are present in the provided set.
    /// </summary>
    /// <remarks>
    /// Performs a single <c>WHERE Code IN (...)</c> query, used by the batch-insert
    /// service to detect duplicate codes before attempting insertion.
    /// Comparison is case-insensitive.
    /// </remarks>
    /// <param name="codes">The IATA codes to look up.</param>
    /// <returns>
    /// A (possibly empty) collection of <see cref="Airport"/> entities whose codes
    /// match any value in <paramref name="codes"/>.
    /// </returns>
    Task<IEnumerable<Airport>> GetByCodesAsync(IEnumerable<string> codes);
}
