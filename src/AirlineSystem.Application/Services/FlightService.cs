using System.Globalization;
using AirlineSystem.Application.DTOs.Flights;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Domain.Entities;
using CsvHelper;
using CsvHelper.Configuration;

namespace AirlineSystem.Application.Services;

public class FlightService : IFlightService
{
    private readonly IUnitOfWork _uow;

    public FlightService(IUnitOfWork uow) => _uow = uow;

    public async Task<(int SuccessCount, List<string> Errors)> UploadFlightsFromCsvAsync(Stream csvStream)
    {
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

    public async Task<IEnumerable<FlightDto>> GetAllAsync()
    {
        var flights = await _uow.Flights.GetAllAsync();
        return flights.Select(f => ToDto(f));
    }

    public async Task<FlightDto?> GetByIdAsync(Guid id)
    {
        var flight = await _uow.Flights.GetByIdAsync(id);
        return flight is null ? null : ToDto(flight);
    }

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
