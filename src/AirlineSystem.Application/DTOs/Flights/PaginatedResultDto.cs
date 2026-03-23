namespace AirlineSystem.Application.DTOs.Flights;

public class PaginatedResultDto<T>
{
    public const int PageSize = 10;

    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PaginatedResultDto<T> Create(IEnumerable<T> items, int totalCount, int pageNumber) =>
        new() { Items = items, TotalCount = totalCount, PageNumber = pageNumber };
}
