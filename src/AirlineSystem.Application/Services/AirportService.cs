using AirlineSystem.Application.DTOs.Airports;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Application.Services;

/// <summary>
/// Implements airport management operations (FR-02).
/// Provides standard CRUD for the <see cref="Airport"/> entity, restricted to
/// Admin callers at the API layer.
/// </summary>
public class AirportService : IAirportService
{
    private readonly IUnitOfWork _uow;

    /// <summary>
    /// Initialises the service with the shared unit of work.
    /// </summary>
    public AirportService(IUnitOfWork uow) => _uow = uow;

    /// <inheritdoc/>
    /// <remarks>
    /// <b>POST-CONDITIONS:</b> No state is mutated; this is a read-only operation.
    /// </remarks>
    public async Task<IEnumerable<AirportDto>> GetAllAsync()
    {
        var airports = await _uow.Airports.GetAllAsync();
        return airports.Select(ToDto);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>POST-CONDITIONS:</b> No state is mutated; this is a read-only operation.
    /// </remarks>
    public async Task<AirportDto?> GetByIdAsync(Guid id)
    {
        var airport = await _uow.Airports.GetByIdAsync(id);
        return airport is null ? null : ToDto(airport);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>PRE-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>No existing airport shares the provided <c>Code</c>.</item>
    /// </list>
    /// <b>POST-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>A new <see cref="Airport"/> entity is persisted with a system-generated GUID.</item>
    /// </list>
    /// </remarks>
    public async Task<AirportDto> CreateAsync(AirportRequestDto dto)
    {
        var existing = await _uow.Airports.GetByCodeAsync(dto.Code);
        if (existing is not null)
            throw new InvalidOperationException($"An airport with code '{dto.Code}' already exists.");

        var airport = new Airport
        {
            Id = Guid.NewGuid(),
            Code = dto.Code.ToUpper(),
            Name = dto.Name,
            City = dto.City,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Airports.AddAsync(airport);
        await _uow.SaveChangesAsync();

        return ToDto(airport);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>PRE-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>An <see cref="Airport"/> with the given <paramref name="id"/> must exist.</item>
    /// </list>
    /// <b>POST-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>All three mutable fields (<c>Code</c>, <c>Name</c>, <c>City</c>) are replaced.</item>
    /// </list>
    /// </remarks>
    public async Task UpdateAsync(Guid id, AirportRequestDto dto)
    {
        var airport = await _uow.Airports.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Airport with id '{id}' not found.");

        airport.Code = dto.Code.ToUpper();
        airport.Name = dto.Name;
        airport.City = dto.City;

        _uow.Airports.Update(airport);
        await _uow.SaveChangesAsync();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>PRE-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>An <see cref="Airport"/> with the given <paramref name="id"/> must exist.</item>
    /// </list>
    /// <b>POST-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>The airport record is permanently removed from the database.</item>
    ///   <item>Deletion is blocked by the database if any <see cref="Flight"/> still
    ///   references this airport (<c>DeleteBehavior.Restrict</c> configured in
    ///   <c>AirlineDbContext</c>).</item>
    /// </list>
    /// </remarks>
    public async Task DeleteAsync(Guid id)
    {
        var airport = await _uow.Airports.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Airport with id '{id}' not found.");

        _uow.Airports.Delete(airport);
        await _uow.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<AirportBatchResponseDto> CreateBatchAsync(IEnumerable<AirportRequestDto> dtos)
    {
        var list = dtos.ToList();

        if (list.Count == 0)
            throw new ArgumentException("The airport list must not be empty.");

        // S0 → S1: deduplicate within the batch (first occurrence wins)
        var skipped = new List<string>();
        var seen    = new HashSet<string>();
        var unique  = new List<AirportRequestDto>();

        foreach (var dto in list)
        {
            var upper = dto.Code.ToUpper();
            if (!seen.Add(upper))
                skipped.Add(upper);
            else
                unique.Add(dto);
        }

        // S1 → S2: single query to find codes that already exist in the DB
        var dbExisting = await _uow.Airports.GetByCodesAsync(seen);
        var dbCodes    = dbExisting.Select(a => a.Code).ToHashSet();

        var toInsert = unique.Where(d => !dbCodes.Contains(d.Code.ToUpper())).ToList();
        skipped.AddRange(dbCodes);

        // S2 → S3: persist only the new records (skip entirely if nothing is new)
        var now      = DateTime.UtcNow;
        var airports = toInsert.Select(dto => new Airport
        {
            Id        = Guid.NewGuid(),
            Code      = dto.Code.ToUpper(),
            Name      = dto.Name,
            City      = dto.City,
            CreatedAt = now
        }).ToList();

        if (airports.Count > 0)
        {
            await _uow.Airports.AddRangeAsync(airports);
            await _uow.SaveChangesAsync();
        }

        var message = skipped.Count > 0
            ? $"Successfully added {airports.Count} airport(s). Skipped {skipped.Count} duplicate(s): {string.Join(", ", skipped)}."
            : $"Successfully added {airports.Count} airport(s).";

        return new AirportBatchResponseDto
        {
            Message      = message,
            Airports     = airports.Select(ToDto),
            SkippedCodes = skipped
        };
    }

    private static AirportDto ToDto(Airport a) =>
        new() { Id = a.Id, Code = a.Code, Name = a.Name, City = a.City };
}
