namespace AirlineSystem.Application.DTOs.Flights;

/// <summary>
/// Wraps the result of a flight search. When <c>IsRoundTrip</c> is <c>false</c>,
/// only <see cref="Outbound"/> is populated. When <c>true</c>, both
/// <see cref="Outbound"/> and <see cref="ReturnFlights"/> are populated with the
/// outbound (A→B) and return (B→A) legs respectively.
/// </summary>
public class FlightSearchResponseDto
{
    /// <summary>Paginated outbound flights matching the requested route and date range.</summary>
    public PaginatedResultDto<FlightDto> Outbound { get; set; } = null!;

    /// <summary>
    /// Paginated return flights (reversed route) when <c>IsRoundTrip = true</c>;
    /// <c>null</c> otherwise.
    /// </summary>
    public PaginatedResultDto<FlightDto>? ReturnFlights { get; set; }
}
