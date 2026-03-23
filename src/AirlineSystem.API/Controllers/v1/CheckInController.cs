using AirlineSystem.Application.DTOs.CheckIn;
using AirlineSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineSystem.API.Controllers.v1;

/// <summary>
/// Handles passenger check-in with sequential seat assignment (FR-06).
/// This endpoint is public and does not require authentication (FR-06.04).
/// </summary>
[ApiController]
[Route("api/v1/checkin")]
[AllowAnonymous]
public class CheckInController : ControllerBase
{
    private readonly ICheckInService _checkInService;

    /// <summary>Initialises the controller with the check-in service.</summary>
    public CheckInController(ICheckInService checkInService) => _checkInService = checkInService;

    /// <summary>
    /// Performs check-in for a named passenger on a specified flight (FR-06).
    /// </summary>
    /// <remarks>
    /// The endpoint always returns HTTP 200 — business rejections (no ticket found,
    /// already checked in) are communicated via <c>Status = "Failed"</c> in the
    /// response body, not via an error status code (FR-06.03). On success,
    /// the next sequential seat number is assigned and persisted.
    /// </remarks>
    /// <param name="request">
    /// Check-in payload: flight number, departure date, and the passenger's full name
    /// as recorded at booking time.
    /// </param>
    /// <returns>
    /// A <see cref="CheckInResponseDto"/> with <c>Status = "Success"</c> and the
    /// assigned <c>SeatNumber</c> on success, or <c>Status = "Failed"</c> with a
    /// descriptive <c>Message</c> when check-in is rejected.
    /// </returns>
    /// <response code="200">Check-in result returned (inspect <c>Status</c> field).</response>
    [HttpPost]
    [ProducesResponseType(typeof(CheckInResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequestDto request)
    {
        var result = await _checkInService.CheckInPassengerAsync(request);
        return Ok(result);
    }
}
