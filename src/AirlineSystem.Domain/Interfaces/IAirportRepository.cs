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
}
