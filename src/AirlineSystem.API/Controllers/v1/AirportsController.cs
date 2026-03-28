using AirlineSystem.Application.DTOs.Airports;
using AirlineSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineSystem.API.Controllers.v1;

/// <summary>
/// Provides CRUD management endpoints for Airport entities (FR-02).
/// All endpoints are restricted to the <c>Admin</c> role.
/// </summary>
[ApiController]
[Route("api/v1/airports")]
[Authorize(Roles = "Admin")]
public class AirportsController : ControllerBase
{
    private readonly IAirportService _airportService;

    /// <summary>Initialises the controller with the airport service.</summary>
    public AirportsController(IAirportService airportService) =>
        _airportService = airportService;

    /// <summary>Returns all airports in the system.</summary>
    /// <returns>A list of all <see cref="AirportDto"/> records.</returns>
    /// <response code="200">List returned (may be empty).</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AirportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll()
    {
        var airports = await _airportService.GetAllAsync();
        return Ok(airports);
    }

    /// <summary>Retrieves a single airport by its internal GUID.</summary>
    /// <param name="id">The airport's primary key.</param>
    /// <returns>The matching airport, or 404 if not found.</returns>
    /// <response code="200">Airport found and returned.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    /// <response code="404">No airport exists with the given <paramref name="id"/>.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AirportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var airport = await _airportService.GetByIdAsync(id);
        if (airport is null) return NotFound();
        return Ok(airport);
    }

    /// <summary>
    /// Creates a new airport record.
    /// </summary>
    /// <param name="dto">Airport data: IATA code, name, and city.</param>
    /// <returns>The created airport with its generated <c>Id</c>.</returns>
    /// <response code="201">Airport created successfully.</response>
    /// <response code="400">An airport with the same IATA code already exists.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    [HttpPost]
    [ProducesResponseType(typeof(AirportDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] AirportRequestDto dto)
    {
        var created = await _airportService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing airport record.
    /// </summary>
    /// <param name="id">The internal GUID of the airport to update.</param>
    /// <param name="dto">Replacement airport data.</param>
    /// <response code="204">Update successful; no content returned.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    /// <response code="404">No airport exists with the given <paramref name="id"/>.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] AirportRequestDto dto)
    {
        await _airportService.UpdateAsync(id, dto);
        return NoContent();
    }

    /// <summary>
    /// Creates multiple airport records in a single batch transaction using
    /// <b>insert-ignore</b> semantics.
    /// </summary>
    /// <param name="dtos">
    /// A non-empty JSON array of airport objects. Each element must include
    /// <c>code</c> (IATA), <c>name</c>, and <c>city</c>.
    /// </param>
    /// <returns>
    /// An <see cref="AirportBatchResponseDto"/> containing a human-readable
    /// <c>message</c>, the <c>airports</c> that were newly persisted, and the
    /// <c>skippedCodes</c> that were already present (intra-batch or in the DB).
    /// </returns>
    /// <remarks>
    /// Duplicate IATA codes are never treated as an error. They are silently skipped
    /// and reported in <c>skippedCodes</c>. The response is always <c>200 OK</c>,
    /// even when every supplied code already existed and <c>airports</c> is empty.
    /// </remarks>
    /// <response code="200">
    /// Batch processed successfully. Inspect <c>airports</c> for inserted records
    /// and <c>skippedCodes</c> for any codes that were ignored.
    /// </response>
    /// <response code="400">The supplied list is empty.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(AirportBatchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateBatch([FromBody] IEnumerable<AirportRequestDto> dtos)
    {
        var result = await _airportService.CreateBatchAsync(dtos);
        return Ok(result);
    }

    /// <summary>
    /// Permanently deletes an airport record.
    /// </summary>
    /// <param name="id">The internal GUID of the airport to delete.</param>
    /// <response code="204">Deletion successful; no content returned.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    /// <response code="404">No airport exists with the given <paramref name="id"/>.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _airportService.DeleteAsync(id);
        return NoContent();
    }
}
