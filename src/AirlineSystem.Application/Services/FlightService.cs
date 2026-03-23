using System.Globalization;
using AirlineSystem.Application.DTOs.Flights;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Domain.Entities;
using CsvHelper;
using CsvHelper.Configuration;

namespace AirlineSystem.Application.Services;

/// <summary>
/// Implements flight inventory management (FR-02, FR-03) and public flight search (FR-04).
/// Admin-scoped methods are secured at the API layer via <c>[Authorize(Roles = "Admin")]</c>.
/// </summary>
public class FlightService : IFlightService
{
    private readonly IUnitOfWork _uow;

    public FlightService(IUnitOfWork uow) => _uow = uow;

    /// <inheritdoc/>
    /// <remarks>
    /// <b>PRE-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>Caller must be an authenticated <c>Admin</c> (FR-03.04).</item>
    ///   <item><paramref name="csvStream"/> must be a readable, UTF-8 encoded CSV stream
    ///   with a header row whose column names case-insensitively match <see cref="FlightUploadDto"/>
    ///   property names.</item>
    /// </list>
    /// <b>POST-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>Only rows that pass all validation checks are persisted.</item>
    ///   <item>All valid rows are committed in a single <c>SaveChangesAsync</c> call (atomic batch).</item>
    ///   <item>Rows that fail validation are skipped; their errors are returned to the caller.</item>
    /// </list>
    /// <b>BUSINESS RULES (per row):</b>
    /// <list type="bullet">
    ///   <item><c>(ArrivalDate - DepartureDate).TotalMinutes</c> must equal
    ///   <c>DurationMinutes</c> within a ±1 minute tolerance (confirmed architectural decision).</item>
    ///   <item>A flight with the same <c>(FlightNumber, DepartureDate)</c> must not already
    ///   exist in the database (FR-03.02 duplicate prevention).</item>
    ///   <item>Both <c>OriginAirportCode</c> and <c>DestinationAirportCode</c> must resolve
    ///   to existing <see cref="Airport"/> records.</item>
    ///   <item><c>AvailableCapacity</c> is initialised equal to <c>TotalCapacity</c>
    ///   on creation.</item>
    /// </list>
    /// </remarks>
    public async Task<(int SuccessCount, List<string> Errors)> UploadFlightsFromCsvAsync(Stream csvStream)
    {
        ArgumentNullException.ThrowIfNull(csvStream);

        var errors = new List<string>();
        var successCount = 0;

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null
        };

        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, config);

        IEnumerable<FlightUploadDto> records;
        try
        {
            records = csv.GetRecords<FlightUploadDto>().ToList();
        }
        catch (Exception ex)
        {
            return (0, new List<string> { $"CSV parse error: {ex.Message}" });
        }

        foreach (var row in records)
        {
            var rowLabel = $"[{row.FlightNumber} / {row.DepartureDate:yyyy-MM-dd}]";

            var expectedMinutes = (row.ArrivalDate - row.DepartureDate).TotalMinutes;
            if (Math.Abs(expectedMinutes - row.DurationMinutes) > 1)
            {
                errors.Add($"{rowLabel} Duration mismatch: declared {row.DurationMinutes} min but ArrivalDate-DepartureDate = {expectedMinutes} min.");
                continue;
            }

            if (await _uow.Flights.ExistsAsync(row.FlightNumber, row.DepartureDate))
            {
                errors.Add($"{rowLabel} Duplicate flight — already exists in the database.");
                continue;
            }

            var origin = await _uow.Airports.GetByCodeAsync(row.OriginAirportCode);
            if (origin is null)
            {
                errors.Add($"{rowLabel} Origin airport code '{row.OriginAirportCode}' not found.");
                continue;
            }

            var destination = await _uow.Airports.GetByCodeAsync(row.DestinationAirportCode);
            if (destination is null)
            {
                errors.Add($"{rowLabel} Destination airport code '{row.DestinationAirportCode}' not found.");
                continue;
            }

            var flight = new Flight
            {
                Id = Guid.NewGuid(),
                FlightNumber = row.FlightNumber,
                DepartureDate = row.DepartureDate,
                ArrivalDate = row.ArrivalDate,
                DurationMinutes = row.DurationMinutes,
                TotalCapacity = row.TotalCapacity,
                AvailableCapacity = row.TotalCapacity,
                OriginAirportId = origin.Id,
                DestinationAirportId = destination.Id,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.Flights.AddAsync(flight);
            successCount++;
        }

        if (successCount > 0)
            await _uow.SaveChangesAsync();

        return (successCount, errors);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>PRE-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>No authentication required (FR-04.06).</item>
    ///   <item><c>NumberOfPeople</c> must be &gt;= 1.</item>
    ///   <item><c>DepartureTo</c> must be &gt;= <c>DepartureFrom</c>.</item>
    /// </list>
    /// <b>POST-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>Only flights where <c>AvailableCapacity &gt;= NumberOfPeople</c> are returned (FR-04.03).</item>
    ///   <item>Results are paginated with a fixed page size of 10 (FR-04.05).</item>
    ///   <item>No state is mutated.</item>
    /// </list>
    /// </remarks>
    public async Task<PaginatedResultDto<FlightDto>> SearchFlightsAsync(FlightSearchRequestDto request)
    {
        var (flights, totalCount) = await _uow.Flights.SearchFlightsAsync(
            request.OriginCode,
            request.DestinationCode,
            request.DepartureFrom,
            request.DepartureTo,
            request.NumberOfPeople,
            request.PageNumber);

        var items = flights.Select(f => ToDto(f));
        return PaginatedResultDto<FlightDto>.Create(items, totalCount, request.PageNumber);
    }

    /// <inheritdoc/>
    /// <remarks>No pagination. Intended for admin use only (FR-02.02).</remarks>
    public async Task<IEnumerable<FlightDto>> GetAllAsync()
    {
        var flights = await _uow.Flights.GetAllAsync();
        return flights.Select(f => ToDto(f));
    }

    /// <inheritdoc/>
    public async Task<FlightDto?> GetByIdAsync(Guid id)
    {
        var flight = await _uow.Flights.GetByIdAsync(id);
        return flight is null ? null : ToDto(flight);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>POST-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item><c>AvailableCapacity</c> is initialised equal to <c>TotalCapacity</c>.</item>
    ///   <item>Both <c>OriginAirportId</c> and <c>DestinationAirportId</c> are resolved
    ///   from their IATA codes before the entity is persisted.</item>
    /// </list>
    /// </remarks>
    public async Task<FlightDto> CreateAsync(FlightUploadDto dto)
    {
        var origin = await _uow.Airports.GetByCodeAsync(dto.OriginAirportCode)
            ?? throw new KeyNotFoundException($"Origin airport '{dto.OriginAirportCode}' not found.");

        var destination = await _uow.Airports.GetByCodeAsync(dto.DestinationAirportCode)
            ?? throw new KeyNotFoundException($"Destination airport '{dto.DestinationAirportCode}' not found.");

        var flight = new Flight
        {
            Id = Guid.NewGuid(),
            FlightNumber = dto.FlightNumber,
            DepartureDate = dto.DepartureDate,
            ArrivalDate = dto.ArrivalDate,
            DurationMinutes = dto.DurationMinutes,
            TotalCapacity = dto.TotalCapacity,
            AvailableCapacity = dto.TotalCapacity,
            OriginAirportId = origin.Id,
            DestinationAirportId = destination.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Flights.AddAsync(flight);
        await _uow.SaveChangesAsync();

        return ToDto(flight, origin.Code, destination.Code);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>BUSINESS RULE:</b> <c>AvailableCapacity</c> is intentionally not updated here
    /// to avoid overwriting runtime seat availability. Only schedule fields are mutated.
    /// </remarks>
    public async Task UpdateAsync(Guid id, FlightUploadDto dto)
    {
        var flight = await _uow.Flights.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Flight with id '{id}' not found.");

        var origin = await _uow.Airports.GetByCodeAsync(dto.OriginAirportCode)
            ?? throw new KeyNotFoundException($"Origin airport '{dto.OriginAirportCode}' not found.");

        var destination = await _uow.Airports.GetByCodeAsync(dto.DestinationAirportCode)
            ?? throw new KeyNotFoundException($"Destination airport '{dto.DestinationAirportCode}' not found.");

        flight.FlightNumber = dto.FlightNumber;
        flight.DepartureDate = dto.DepartureDate;
        flight.ArrivalDate = dto.ArrivalDate;
        flight.DurationMinutes = dto.DurationMinutes;
        flight.TotalCapacity = dto.TotalCapacity;
        flight.OriginAirportId = origin.Id;
        flight.DestinationAirportId = destination.Id;

        _uow.Flights.Update(flight);
        await _uow.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id)
    {
        var flight = await _uow.Flights.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Flight with id '{id}' not found.");

        _uow.Flights.Delete(flight);
        await _uow.SaveChangesAsync();
    }

    private static FlightDto ToDto(Flight f, string? originCode = null, string? destCode = null) =>
        new()
        {
            Id = f.Id,
            FlightNumber = f.FlightNumber,
            DepartureDate = f.DepartureDate,
            ArrivalDate = f.ArrivalDate,
            DurationMinutes = f.DurationMinutes,
            TotalCapacity = f.TotalCapacity,
            AvailableCapacity = f.AvailableCapacity,
            OriginAirportCode = originCode ?? f.OriginAirport?.Code ?? string.Empty,
            DestinationAirportCode = destCode ?? f.DestinationAirport?.Code ?? string.Empty
        };
}
