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

    /// <summary>
    /// Inserts multiple airports in a single transaction using <b>insert-ignore</b> semantics.
    /// Duplicate IATA codes — whether repeated within the request payload or already present
    /// in the database — are silently skipped rather than rejected.
    /// </summary>
    /// <param name="dtos">
    /// A non-empty list of airport records to create. Each item must supply
    /// <c>Code</c>, <c>Name</c>, and <c>City</c>.
    /// </param>
    /// <returns>
    /// An <see cref="AirportBatchResponseDto"/> that contains:
    /// <list type="bullet">
    ///   <item>A human-readable <c>Message</c> summarising how many records were inserted
    ///   and how many were skipped.</item>
    ///   <item>The <c>Airports</c> collection of newly persisted records (may be empty when
    ///   all provided codes already existed).</item>
    ///   <item>The <c>SkippedCodes</c> collection of IATA codes that were not inserted
    ///   (empty when every code was new).</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <b>PRE-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item><paramref name="dtos"/> contains at least one entry.</item>
    /// </list>
    /// <b>POST-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>Only records whose codes do not already exist in the database are persisted,
    ///   in a single <c>SaveChangesAsync</c> call.</item>
    ///   <item>If all codes were duplicates, no database write is issued and the returned
    ///   <c>Airports</c> collection is empty.</item>
    /// </list>
    /// <b>BUSINESS RULES:</b>
    /// <list type="bullet">
    ///   <item>Codes are normalised to upper-case before insertion and before duplicate
    ///   comparison.</item>
    ///   <item>When the same code appears more than once in <paramref name="dtos"/>, only the
    ///   first occurrence is kept; subsequent occurrences are treated as intra-batch
    ///   duplicates and added to <c>SkippedCodes</c>.</item>
    ///   <item>Codes that already exist in the database are also added to
    ///   <c>SkippedCodes</c> via a single <c>WHERE Code IN (...)</c> query.</item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="dtos"/> contains no elements.
    /// </exception>
    Task<AirportBatchResponseDto> CreateBatchAsync(IEnumerable<AirportRequestDto> dtos);
}
