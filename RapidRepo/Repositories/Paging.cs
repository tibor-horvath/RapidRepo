using RapidRepo.Dtos;
using RapidRepo.Entities;

namespace RapidRepo.Repositories;

/// <summary>
/// The paging rules of the repository contract, shared by every provider: argument validation, the offset of a
/// page, the order paging falls back to, and building the <see cref="Paged{T}"/> result.
/// </summary>
public static class Paging
{
    /// <summary>
    /// Validates a page request.
    /// </summary>
    /// <param name="pageIndex">The 1-based page number.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="pageIndex"/> or <paramref name="pageSize"/> is less than 1, or the page starts beyond
    /// <see cref="int.MaxValue"/> items.
    /// </exception>
    public static void EnsureValid(int pageIndex, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageIndex, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        if ((long)(pageIndex - 1) * pageSize > int.MaxValue)
            throw new ArgumentOutOfRangeException(
                nameof(pageIndex),
                pageIndex,
                $"Page {pageIndex} of size {pageSize} starts beyond {int.MaxValue} items.");
    }

    /// <summary>
    /// Gets the number of items before the page. Call <see cref="EnsureValid"/> first.
    /// </summary>
    /// <param name="pageIndex">The 1-based page number.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <returns>The number of items to skip.</returns>
    public static int Offset(int pageIndex, int pageSize) => (pageIndex - 1) * pageSize;

    /// <summary>
    /// Gets the order paging falls back to when the caller gives none, so that pages are stable: ascending by
    /// <see cref="BaseEntity{TId}.Id"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <returns>The default page order.</returns>
    public static SortOrder<TEntity> DefaultOrder<TEntity, TKey>()
        where TEntity : BaseEntity<TKey>
        where TKey : notnull
        => DefaultOrderCache<TEntity, TKey>.Order;

    /// <summary>
    /// Builds the result of a paged read.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="totalCount">The number of items across all pages.</param>
    /// <param name="results">The items on the page.</param>
    /// <param name="pageIndex">The 1-based page number.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <returns>The page.</returns>
    public static Paged<T> ToPage<T>(int totalCount, IList<T> results, int pageIndex, int pageSize)
        where T : class =>
        new()
        {
            Results = results,
            TotalCount = totalCount,
            Page = pageIndex,
            PageSize = pageSize
        };

    private static class DefaultOrderCache<TEntity, TKey>
        where TEntity : BaseEntity<TKey>
        where TKey : notnull
    {
        internal static readonly SortOrder<TEntity> Order = SortOrder<TEntity>.By(e => e.Id);
    }
}
