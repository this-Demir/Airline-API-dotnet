namespace AirlineSystem.Application.DTOs.Airports;

/// <summary>
/// Request DTO for creating or updating an airport. The system assigns the
/// primary key; callers must not supply an <c>Id</c>.
/// </summary>
public class AirportRequestDto
{
    /// <summary>IATA airport code (e.g., <c>"IST"</c>, <c>"ADB"</c>). Must be unique.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Full airport name (e.g., <c>"Istanbul Airport"</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>City where the airport is located.</summary>
    public string City { get; set; } = string.Empty;
}
