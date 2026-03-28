namespace AirlineSystem.Application.DTOs.Airports;

/// <summary>
/// Response DTO returned by the airport batch-insert endpoint
/// (<c>POST /api/v1/airports/batch</c>).
/// </summary>
/// <remarks>
/// The endpoint uses <b>insert-ignore</b> semantics: duplicate codes are never an
/// error — they are silently skipped and reported in <see cref="SkippedCodes"/>.
/// Clients can inspect <see cref="SkippedCodes"/> to determine which records were
/// already present and which were newly persisted.
/// </remarks>
public class AirportBatchResponseDto
{
    /// <summary>
    /// Human-readable outcome summary.
    /// Examples:
    /// <list type="bullet">
    ///   <item><c>"Successfully added 3 airport(s)."</c> — all records were new.</item>
    ///   <item><c>"Successfully added 2 airport(s). Skipped 1 duplicate(s): BCH."</c> — partial insert.</item>
    ///   <item><c>"Successfully added 0 airport(s). Skipped 2 duplicate(s): BCH, JFK."</c> — all already existed.</item>
    /// </list>
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The newly created airport records (empty when all codes were duplicates).</summary>
    public IEnumerable<AirportDto> Airports { get; set; } = [];

    /// <summary>
    /// IATA codes that were present either more than once in the request payload
    /// (intra-batch) or already exist in the database. These codes were not inserted.
    /// Empty when every code in the request was new.
    /// </summary>
    public IEnumerable<string> SkippedCodes { get; set; } = [];
}
