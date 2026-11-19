using System.Linq.Expressions;

namespace RapidRepo.Repositories;

/// <summary>
/// A declarative sort order for <typeparamref name="TEntity"/>: an ordered list of key selectors, each ascending or
/// descending.
/// </summary>
/// <remarks>
/// Unlike a <c>Func&lt;IQueryable&lt;T&gt;, IOrderedQueryable&lt;T&gt;&gt;</c>, a sort order can be inspected, so any
/// provider can translate it, whether to a LINQ <c>OrderBy</c> chain or to an SQL <c>ORDER BY</c> clause.
/// Instances are immutable: <see cref="ThenBy{TKey}"/> and <see cref="ThenByDescending{TKey}"/> return a new order.
/// </remarks>
/// <example>
/// <code>
/// var order = SortOrder&lt;Employee&gt;.By(e =&gt; e.LastName).ThenByDescending(e =&gt; e.StartDate);
/// </code>
/// </example>
/// <typeparam name="TEntity">The type of the entity being sorted.</typeparam>
public sealed class SortOrder<TEntity>
{
    private readonly SortKey[] _keys;

    private SortOrder(SortKey[] keys) => _keys = keys;

    /// <summary>
    /// Gets the sort keys, most significant first.
    /// </summary>
    public IReadOnlyList<SortKey> Keys => _keys;

    /// <summary>
    /// Creates an ascending sort order on <paramref name="key"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the sort key.</typeparam>
    /// <param name="key">The key selector.</param>
    /// <returns>The sort order.</returns>
    public static SortOrder<TEntity> By<TKey>(Expression<Func<TEntity, TKey>> key) =>
        new([new SortKey(Require(key), Descending: false)]);

    /// <summary>
    /// Creates a descending sort order on <paramref name="key"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the sort key.</typeparam>
    /// <param name="key">The key selector.</param>
    /// <returns>The sort order.</returns>
    public static SortOrder<TEntity> ByDescending<TKey>(Expression<Func<TEntity, TKey>> key) =>
        new([new SortKey(Require(key), Descending: true)]);

    /// <summary>
    /// Returns a new sort order that additionally sorts ascending on <paramref name="key"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the sort key.</typeparam>
    /// <param name="key">The key selector.</param>
    /// <returns>The extended sort order.</returns>
    public SortOrder<TEntity> ThenBy<TKey>(Expression<Func<TEntity, TKey>> key) =>
        new([.. _keys, new SortKey(Require(key), Descending: false)]);

    /// <summary>
    /// Returns a new sort order that additionally sorts descending on <paramref name="key"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the sort key.</typeparam>
    /// <param name="key">The key selector.</param>
    /// <returns>The extended sort order.</returns>
    public SortOrder<TEntity> ThenByDescending<TKey>(Expression<Func<TEntity, TKey>> key) =>
        new([.. _keys, new SortKey(Require(key), Descending: true)]);

    private static LambdaExpression Require(LambdaExpression key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key;
    }
}

/// <summary>
/// One key of a <see cref="SortOrder{TEntity}"/>.
/// </summary>
/// <param name="Key">The key selector, a lambda taking the entity and returning the key.</param>
/// <param name="Descending">Whether the key sorts descending.</param>
public readonly record struct SortKey(LambdaExpression Key, bool Descending);
