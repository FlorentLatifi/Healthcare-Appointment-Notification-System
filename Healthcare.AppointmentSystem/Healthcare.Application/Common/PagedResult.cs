namespace Healthcare.Application.Common;

/// <summary>
/// Represents a paginated result set.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
/// <remarks>
/// Design Pattern: Data Transfer Object (DTO) + Pagination Pattern
/// 
/// This provides:
/// - Items for current page
/// - Metadata for navigation (total count, page info)
/// - HATEOAS-ready structure
/// </remarks>
public sealed class PagedResult<T>
{
    /// <summary>
    /// Gets the items on the current page.
    /// </summary>
    public IEnumerable<T> Items { get; }

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the page size (number of items per page).
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Gets the total number of items across all pages.
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages { get; }

    /// <summary>
    /// Gets whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Gets whether there is a next page.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>
    /// Initializes a new instance of PagedResult.
    /// </summary>
    public PagedResult(
        IEnumerable<T> items,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        Items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    /// <summary>
    /// Creates a paged result from a full collection.
    /// </summary>
    public static PagedResult<T> Create(
        IEnumerable<T> source,
        int pageNumber,
        int pageSize)
    {
        var items = source as IList<T> ?? source.ToList();
        var totalCount = items.Count;

        var pagedItems = items
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<T>(pagedItems, pageNumber, pageSize, totalCount);
    }
}