namespace Healthcare.Application.Common;

/// <summary>
/// Represents pagination query parameters.
/// </summary>
public sealed class PaginationParameters
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    private int _pageSize = DefaultPageSize;

    /// <summary>
    /// Gets or sets the page number (1-based).
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size (number of items per page).
    /// Maximum allowed: 100.
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }
}