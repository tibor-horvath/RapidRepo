namespace RapidRepo.Repositories;

/// <summary>
/// Provider-neutral options for a read through <see cref="Interfaces.IReadOnlyRepository{TEntity, TKey}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Providers may accept a subclass that carries provider-specific hints, such as <c>EfReadOptions&lt;TEntity&gt;</c>
/// in RapidRepo.EntityFrameworkCore. A provider that receives a subclass it does not recognise honours the members
/// declared here and ignores the rest, so passing another provider's options is never an error.
/// </para>
/// <para>
/// New options are added as properties, so adding one never changes a repository signature.
/// </para>
/// </remarks>
public class ReadOptions
{
    /// <summary>
    /// The default options: soft-deleted entities are excluded.
    /// </summary>
    public static readonly ReadOptions Default = new();

    /// <summary>
    /// Gets a value indicating whether soft-deleted entities (those implementing
    /// <see cref="Entities.Interfaces.IDeletableEntity"/> with a <c>DeletedAt</c> value) are included in the results.
    /// </summary>
    /// <remarks>
    /// This only concerns soft delete. Other provider-level filters, such as tenant isolation, are meant to stay in
    /// force; a provider that cannot separate the two documents what it bypasses.
    /// </remarks>
    public bool IncludeDeleted { get; init; }
}
