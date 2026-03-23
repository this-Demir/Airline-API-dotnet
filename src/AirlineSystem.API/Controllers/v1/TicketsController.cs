using System.Security.Claims;
using AirlineSystem.Application.DTOs.Tickets;
using AirlineSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineSystem.API.Controllers.v1;

/// <summary>
/// Handles atomic ticket purchasing for authenticated customers (FR-05).
/// </summary>
[ApiController]
[Route("api/v1/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;

    /// <summary>Initialises the controller with the ticket service.</summary>
    public TicketsController(ITicketService ticketService) => _ticketService = ticketService;

    /// <summary>
    /// Purchases one or more tickets on a specific flight (FR-05).
    /// </summary>
    /// <remarks>
    /// The <c>UserId</c> is extracted securely from the JWT <c>sub</c> claim — the
    /// client must never supply it. A single <see cref="Domain.Entities.Booking"/>
    /// groups all passengers under one PNR code. <c>Flight.AvailableCapacity</c>
    /// is decremented atomically; optimistic concurrency (RowVersion) prevents
    /// overselling under concurrent load.
    /// </remarks>
    /// <param name="request">
    /// Booking payload: flight number, departure date, and one or more passenger
    /// full names.
    /// </param>
    /// <returns>
    /// A <see cref="TicketResponseDto"/> with <c>Status = "Confirmed"</c> and the
    /// generated PNR code on success, or <c>Status = "SoldOut"</c> when capacity
    /// is insufficient (FR-05.04).
    /// </returns>
    /// <response code="200">Purchase result returned (check <c>Status</c> field).</response>
    /// <response code="400">Invalid request data or flight has already departed.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="404">No flight found for the given flight number and date.</response>
    [HttpPost("purchase")]
    [ProducesResponseType(typeof(TicketResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Purchase([FromBody] BuyTicketRequestDto request)
    {
        // Extract UserId from the JWT sub claim. The ASP.NET Core JWT Bearer
        // middleware maps the 'sub' claim to ClaimTypes.NameIdentifier automatically.
        // Never trust the client to supply their own UserId.
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User identity claim is missing from the token.");

        var userId = Guid.Parse(userIdStr);

        var result = await _ticketService.BuyTicketAsync(request, userId);
        return Ok(result);
    }
}
