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
///   <item><term><see cref="DateFrom"/></term><description>Today's UTC date</description></item>
///   <item><term><see cref="DateTo"/></term><description>6 months from today (UTC)</description></item>
///   <item><term><see cref="AirportFrom"/></term><description>No filter — all departure airports returned</description></item>
///   <item><term><see cref="AirportTo"/></term><description>No filter — all arrival airports returned</description></item>
///   <item><term><see cref="NumberOfPeople"/></term><description>1</description></item>
///   <item><term><see cref="IsRoundTrip"/></term><description>false</description></item>
///   <item><term><see cref="PageNumber"/></term><description>1</description></item>
/// </list>
/// </remarks>
public class FlightSearchRequestDto
{
    /// <summary>
    /// IATA code of the <b>departure</b> airport (e.g., <c>"IST"</c> for Istanbul,
    /// <c>"JFK"</c> for New York). This is an airport code — not a flight number.
    /// When omitted or <c>null</c>, flights from all departure airports are included.
    /// </summary>
    public string? AirportFrom { get; set; }

    /// <summary>
    /// IATA code of the <b>arrival</b> airport (e.g., <c>"ADB"</c> for Izmir,
    /// <c>"LHR"</c> for London Heathrow). This is an airport code — not a flight number.
    /// When omitted or <c>null</c>, flights to all arrival airports are included.
    /// </summary>
    public string? AirportTo { get; set; }

    /// <summary>
    /// <b>Earliest</b> departure date to include, in <c>yyyy-MM-dd</c> format
    /// (e.g., <c>"2026-06-01"</c>). This is a date boundary, not a place.
    /// Defaults to today's UTC date when omitted.
    /// </summary>
    public string? DateFrom { get; set; }

    /// <summary>
    /// <b>Latest</b> departure date to include, in <c>yyyy-MM-dd</c> format
    /// (e.g., <c>"2026-12-31"</c>). This is a date boundary, not a place.
    /// Defaults to 6 months from today's UTC date when omitted.
    /// </summary>
    public string? DateTo { get; set; }

    /// <summary>
    /// Number of passengers that must be accommodated. Only flights with
    /// <c>AvailableCapacity &gt;= NumberOfPeople</c> are returned (FR-04.03).
    /// Defaults to <c>1</c>.
    /// </summary>
    public int NumberOfPeople { get; set; } = 1;

    /// <summary>
    /// When <c>true</c>, the response also includes a reverse-direction search
    /// (<see cref="AirportTo"/> → <see cref="AirportFrom"/>) in
    /// <c>ReturnFlights</c>. Defaults to <c>false</c> (one-way search).
    /// </summary>
    public bool IsRoundTrip { get; set; }

    /// <summary>1-based page number for paginated results. Defaults to <c>1</c>.</summary>
    public int PageNumber { get; set; } = 1;
}
