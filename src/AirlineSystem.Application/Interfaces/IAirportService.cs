using AirlineSystem.Application.DTOs.Airports;

namespace AirlineSystem.Application.Interfaces;

/// <summary>
/// Defines airport management operations (FR-02). All methods are restricted to
/// the Admin role at the API layer via <c>[Authorize(Roles = "Admin")]</c>.
/// </summary>
public interface IAirportService
{
    /// <summary>Returns all airports in the system.</summary>
    /// <returns>
    /// An unordered collection of all <see cref="AirportDto"/> records.
    /// Returns an empty collection — never <c>null</c> — when no airports exist.
    /// </returns>
    Task<IEnumerable<AirportDto>> GetAllAsync();

    /// <summary>Retrieves a single airport by its internal GUID.</summary>
    /// <param name="id">The airport's primary key.</param>
    /// <returns>The matching <see cref="AirportDto"/>, or <c>null</c> if not found.</returns>
    Task<AirportDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// Creates a new airport record.
    /// </summary>
    /// <param name="dto">Airport data including IATA code, name, and city.</param>
    /// <returns>The newly created <see cref="AirportDto"/> with its generated <c>Id</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an airport with the same <c>Code</c> already exists.
    /// </exception>
    Task<AirportDto> CreateAsync(AirportRequestDto dto);

    /// <summary>Updates an existing airport record.</summary>
    /// <param name="id">The internal GUID of the airport to update.</param>
    /// <param name="dto">Replacement airport data.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the airport is not found.</exception>
    Task UpdateAsync(Guid id, AirportRequestDto dto);

    /// <summary>Permanently deletes an airport record.</summary>
    /// <param name="id">The internal GUID of the airport to delete.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the airport is not found.</exception>
    Task DeleteAsync(Guid id);
}
