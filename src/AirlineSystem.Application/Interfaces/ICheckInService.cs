using AirlineSystem.Application.DTOs.CheckIn;

namespace AirlineSystem.Application.Interfaces;

/// <summary>
/// Defines the passenger check-in operation (FR-06). This endpoint is public
/// and does not require authentication.
/// </summary>
public interface ICheckInService
{
    /// <summary>
    /// Performs check-in for a named passenger on a specified flight,
    /// assigning the next available sequential seat number.
    /// </summary>
    /// <param name="request">
    /// Check-in payload: the booking's PNR code and the passenger's full name.
    /// </param>
    /// <returns>
    /// A <see cref="CheckInResponseDto"/> with one of two outcomes:
    /// <list type="bullet">
    ///   <item>
    ///     <c>Status = "Success"</c> — seat assigned; response includes
    ///     <c>SeatNumber</c> and <c>FullName</c>.
    ///   </item>
    ///   <item>
    ///     <c>Status = "Failed"</c> — check-in rejected; response includes a
    ///     <c>Message</c> explaining the reason (no ticket found, or already checked in).
    ///   </item>
    /// </list>
    /// </returns>
    Task<CheckInResponseDto> CheckInPassengerAsync(CheckInRequestDto request);
}
