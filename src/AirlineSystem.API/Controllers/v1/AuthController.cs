using AirlineSystem.Application.DTOs.Auth;
using AirlineSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineSystem.API.Controllers.v1;

/// <summary>
/// Handles Identity and Access Management (IAM) operations: customer
/// self-registration and credential-based login (FR-01).
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    /// <summary>Initialises the controller with the authentication service.</summary>
    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>
    /// Registers a new customer account and returns a signed JWT.
    /// </summary>
    /// <remarks>
    /// Self-registration is restricted to the <c>Customer</c> role (FR-01.01).
    /// Admin accounts must be provisioned out-of-band.
    /// </remarks>
    /// <param name="request">Registration payload containing email and plain-text password.</param>
    /// <returns>A JWT token and the assigned role (<c>"Customer"</c>).</returns>
    /// <response code="200">Registration successful; JWT returned.</response>
    /// <response code="400">Email is already registered or request data is invalid.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        var result = await _authService.RegisterAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Authenticates a user with email and password, and returns a signed JWT.
    /// </summary>
    /// <remarks>
    /// Both "email not found" and "wrong password" return 401 with the same
    /// message to prevent user enumeration (FR-01.02).
    /// </remarks>
    /// <param name="request">Login payload containing email and plain-text password.</param>
    /// <returns>A JWT token and the authenticated user's role.</returns>
    /// <response code="200">Authentication successful; JWT returned.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);
        return Ok(result);
    }
}
