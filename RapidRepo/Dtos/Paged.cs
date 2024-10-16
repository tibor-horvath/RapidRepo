namespace RapidRepo.Dtos;

/// <summary>
/// Represents a paged collection of items.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public record Paged<T>
    where T : class
{
    /// <summary>
    /// Gets or sets the list of results.
    /// </summary>
    public IList<T> Results { get; set; } = [];

    /// <summary>
    /// Gets or sets the total count of items.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the current page number.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPrevious => Page > 1;

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNext => Page < TotalPages;
}
