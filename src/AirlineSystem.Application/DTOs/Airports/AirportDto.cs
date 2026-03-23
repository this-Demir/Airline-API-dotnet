namespace AirlineSystem.Application.DTOs.Airports;

/// <summary>Response DTO returned for all airport read operations.</summary>
public class AirportDto
{
    /// <summary>Internal primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>IATA airport code (e.g., <c>"IST"</c>, <c>"JFK"</c>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Full airport name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>City where the airport is located.</summary>
    public string City { get; set; } = string.Empty;
}
