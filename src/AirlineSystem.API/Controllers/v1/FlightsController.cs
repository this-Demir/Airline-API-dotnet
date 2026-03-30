using System.Globalization;
using AirlineSystem.Application.DTOs.Flights;
using AirlineSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineSystem.API.Controllers.v1;

/// <summary>
/// Manages flight inventory (FR-02, FR-03), public flight search (FR-04),
/// and the admin passenger manifest (FR-07).
/// </summary>
[ApiController]
[Route("api/v1/flights")]
public class FlightsController : ControllerBase
{
    private readonly IFlightService _flightService;
    private readonly ITicketService _ticketService;

    /// <summary>Initialises the controller with the required services.</summary>
    public FlightsController(IFlightService flightService, ITicketService ticketService)
    {
        _flightService = flightService;
        _ticketService = ticketService;
    }

    // ── Public ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Searches for available flights matching the given route, date range, and
    /// passenger count (FR-04). Results are paginated (page size fixed at 10).
    /// </summary>
    /// <remarks>
    /// <b>All parameters are optional.</b> When the request is sent without any query
    /// string the endpoint automatically applies the following defaults:
    /// <list type="table">
    ///   <listheader><term>Parameter</term><description>Default</description></listheader>
    ///   <item><term><c>DateFrom</c></term><description>Today's UTC date</description></item>
    ///   <item><term><c>DateTo</c></term><description>6 months from today (UTC)</description></item>
    ///   <item><term><c>AirportFrom</c></term><description>All departure airports (no filter)</description></item>
    ///   <item><term><c>AirportTo</c></term><description>All arrival airports (no filter)</description></item>
    ///   <item><term><c>NumberOfPeople</c></term><description>1</description></item>
    ///   <item><term><c>IsRoundTrip</c></term><description>false</description></item>
    ///   <item><term><c>PageNumber</c></term><description>1</description></item>
    /// </list>
    /// <b>Date format:</b> <c>DateFrom</c> and <c>DateTo</c> must be supplied in
    /// <c>yyyy-MM-dd</c> format (e.g., <c>2026-06-01</c>). Any other format returns 400.<br/>
    /// Flights where <c>AvailableCapacity &lt; NumberOfPeople</c> are excluded
    /// from results (FR-04.03). Rate limiting (max 3 calls/day per IP) is enforced
    /// at the API Gateway level (NFR-02.03) and is not implemented in this controller.
    /// </remarks>
    /// <param name="request">
    /// Optional search filters bound from the query string. See remarks for defaults
    /// and the required date format.
    /// </param>
    /// <returns>
    /// A <see cref="FlightSearchResponseDto"/> containing a paginated <c>outbound</c> list
    /// and, when <c>IsRoundTrip=true</c>, a paginated <c>returnFlights</c> list.
    /// </returns>
    /// <response code="200">Search results returned (may be an empty page).</response>
    /// <response code="400">A date parameter was supplied in an unsupported format (expected <c>yyyy-MM-dd</c>).</response>
    [HttpGet("search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FlightSearchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] FlightSearchRequestDto request)
    {
        var result = await _flightService.SearchFlightsAsync(request);
        return Ok(result);
    }

    // ── Admin ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bulk-imports flights from an uploaded CSV file (FR-03).
    /// </summary>
    /// <remarks>
    /// The CSV must contain these headers (case-insensitive): <c>FlightNumber</c>,
    /// <c>DepartureDate</c>, <c>ArrivalDate</c>, <c>DurationMinutes</c>,
    /// <c>OriginAirportCode</c>, <c>DestinationAirportCode</c>, <c>TotalCapacity</c>.
    /// Partial success is supported: valid rows are inserted while invalid rows
    /// accumulate in the <c>errors</c> list.
    /// </remarks>
    /// <param name="file">The CSV file submitted as <c>multipart/form-data</c>.</param>
    /// <returns>
    /// A JSON object with <c>successCount</c> (inserted rows) and <c>errors</c>
    /// (per-row failure descriptions).
    /// </returns>
    /// <response code="200">Processing complete; inspect <c>successCount</c> and <c>errors</c>.</response>
    /// <response code="400">No file provided or file is empty.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    [HttpPost("upload")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "A non-empty CSV file is required." });

        var (successCount, errors) = await _flightService.UploadFlightsFromCsvAsync(file.OpenReadStream());
        return Ok(new { successCount, errors });
    }

    /// <summary>Returns all flights in the system (admin use).</summary>
    /// <returns>An unordered list of all <see cref="FlightDto"/> records.</returns>
    /// <response code="200">List returned (may be empty).</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<FlightDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll()
    {
        var flights = await _flightService.GetAllAsync();
        return Ok(flights);
    }

    /// <summary>Retrieves a single flight by its internal GUID.</summary>
    /// <param name="id">The flight's primary key.</param>
    /// <returns>The matching <see cref="FlightDto"/>, or 404 if not found.</returns>
    /// <response code="200">Flight found and returned.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    /// <response code="404">No flight exists with the given <paramref name="id"/>.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(FlightDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var flight = await _flightService.GetByIdAsync(id);
        if (flight is null) return NotFound();
        return Ok(flight);
    }

    /// <summary>
    /// Creates a single flight record manually (admin use).
    /// </summary>
    /// <param name="dto">Flight schedule data including airport codes and capacity.</param>
    /// <returns>The newly created <see cref="FlightDto"/> with its generated <c>Id</c>.</returns>
    /// <response code="201">Flight created successfully.</response>
    /// <response code="400">Origin or destination airport code not found, or invalid data.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(FlightDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] FlightUploadDto dto)
    {
        var created = await _flightService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates mutable fields of an existing flight (admin use).
    /// </summary>
    /// <param name="id">The internal GUID of the flight to update.</param>
    /// <param name="dto">Replacement flight data.</param>
    /// <response code="204">Update successful; no content returned.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    /// <response code="404">No flight or airport found for the given identifiers.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] FlightUploadDto dto)
    {
        await _flightService.UpdateAsync(id, dto);
        return NoContent();
    }

    /// <summary>
    /// Permanently deletes a flight record (admin use).
    /// </summary>
    /// <param name="id">The internal GUID of the flight to delete.</param>
    /// <response code="204">Deletion successful; no content returned.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    /// <response code="404">No flight exists with the given <paramref name="id"/>.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _flightService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Retrieves the paginated passenger manifest for a specific flight (FR-07).
    /// Page size is fixed at 10.
    /// </summary>
    /// <param name="flightNumber">The airline flight code (e.g., <c>TK1923</c>).</param>
    /// <param name="date">
    /// The departure date of the flight in <c>yyyy-MM-dd</c> format
    /// (e.g., <c>2025-06-15</c>).
    /// </param>
    /// <param name="pageNumber">1-based page number (defaults to 1).</param>
    /// <returns>A paginated list of <see cref="PassengerDto"/> records.</returns>
    /// <response code="200">Manifest returned.</response>
    /// <response code="400">The <paramref name="date"/> string could not be parsed.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    [HttpGet("{flightNumber}/date/{date}/passengers")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PaginatedResultDto<PassengerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPassengers(
        string flightNumber,
        string date,
        [FromQuery] int pageNumber = 1)
    {
        if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            return BadRequest(new { message = $"Invalid date format '{date}'. Use yyyy-MM-dd." });

        var result = await _ticketService.GetPassengerListAsync(flightNumber, parsedDate, pageNumber);
        return Ok(result);
    }
}
