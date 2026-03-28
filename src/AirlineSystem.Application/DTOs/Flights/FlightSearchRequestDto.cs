namespace AirlineSystem.Application.DTOs.Flights;

/// <summary>
/// Query parameters for the public flight search endpoint (FR-04).
/// All fields are optional; sensible defaults apply when omitted.
/// </summary>
/// <remarks>
/// <b>Default behaviour when the request is sent without any parameters:</b>
/// <list type="table">
///   <listheader>
///     <term>Parameter</term><description>Default</description>
///   </listheader>
///   <item><term><see cref="DepartureFrom"/></term><description>Today's UTC date</description></item>
///   <item><term><see cref="DepartureTo"/></term><description>6 months from today (UTC)</description></item>
///   <item><term><see cref="OriginCode"/></term><description>No filter — all origins returned</description></item>
///   <item><term><see cref="DestinationCode"/></term><description>No filter — all destinations returned</description></item>
///   <item><term><see cref="NumberOfPeople"/></term><description>1</description></item>
///   <item><term><see cref="IsRoundTrip"/></term><description>false</description></item>
///   <item><term><see cref="PageNumber"/></term><description>1</description></item>
/// </list>
/// </remarks>
public class FlightSearchRequestDto
{
    /// <summary>
    /// IATA code of the departure airport (e.g., <c>"IST"</c>).
    /// When omitted or <c>null</c>, flights from all origins are included.
    /// </summary>
    public string? OriginCode { get; set; }

    /// <summary>
    /// IATA code of the arrival airport (e.g., <c>"JFK"</c>).
    /// When omitted or <c>null</c>, flights to all destinations are included.
    /// </summary>
    public string? DestinationCode { get; set; }

    /// <summary>
    /// Earliest departure date to include, in <c>yyyy-MM-dd</c> format (e.g., <c>"2025-06-01"</c>).
    /// Defaults to today's UTC date when omitted.
    /// </summary>
    public string? DepartureFrom { get; set; }

    /// <summary>
    /// Latest departure date to include, in <c>yyyy-MM-dd</c> format (e.g., <c>"2025-12-31"</c>).
    /// Defaults to 6 months from today's UTC date when omitted.
    /// </summary>
    public string? DepartureTo { get; set; }

    /// <summary>
    /// Number of passengers that must be accommodated. Only flights with
    /// <c>AvailableCapacity &gt;= NumberOfPeople</c> are returned (FR-04.03).
    /// Defaults to <c>1</c>.
    /// </summary>
    public int NumberOfPeople { get; set; } = 1;

    /// <summary>
    /// When <c>true</c>, the response also includes a reverse-direction search
    /// (<see cref="DestinationCode"/> → <see cref="OriginCode"/>) in
    /// <c>ReturnFlights</c>. Defaults to <c>false</c>.
    /// </summary>
    public bool IsRoundTrip { get; set; }

    /// <summary>1-based page number for paginated results. Defaults to <c>1</c>.</summary>
    public int PageNumber { get; set; } = 1;
}
