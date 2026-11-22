using RapidRepo.Entities;
using RapidRepo.Repositories.Interfaces;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// What a provider supplies to run <see cref="WriteRoutingConformanceTests{TStore}"/>: a repository derived from the
/// provider's own repository class that records calls to its overridable write operations.
/// </summary>
/// <remarks>
/// <para>
/// The write rules call some operations from others: <c>DeleteById</c> calls <c>Delete</c>, <c>Delete</c> of an
/// entity without soft delete calls <c>HardDelete</c>, and so on. Those calls must go through the repository's
/// virtual members, so that a custom repository overriding <c>HardDelete</c> sees every hard delete, not only direct
/// calls.
/// </para>
/// <para>
/// Separate from <see cref="IConformanceStore"/>, so providers that have not added it yet keep compiling and passing.
/// </para>
/// </remarks>
public interface IWriteRoutingConformanceStore : IConformanceStore
{
    /// <summary>
    /// Creates a repository whose writes are staged in <paramref name="session"/>, derived from the class
    /// applications derive their custom repositories from (for EF Core, <c>EfRepository&lt;TEntity, TId&gt;</c>).
    /// </summary>
    /// <remarks>
    /// It overrides <c>Delete</c>, <c>HardDelete</c>, <c>HardDeleteRange</c> and <c>Restore</c>. Each override adds
    /// the member's name to <paramref name="calls"/>, then calls the base implementation.
    /// </remarks>
    /// <param name="session">A session opened by this store.</param>
    /// <param name="calls">Receives the name of each overridden member called, in order.</param>
    /// <typeparam name="TEntity">A conformance entity type.</typeparam>
    /// <typeparam name="TKey">Its key type.</typeparam>
    /// <returns>The repository.</returns>
    IRepository<TEntity, TKey> CreateRecordingRepository<TEntity, TKey>(IConformanceSession session, ICollection<string> calls)
        where TEntity : BaseEntity<TKey>
        where TKey : notnull;
}
