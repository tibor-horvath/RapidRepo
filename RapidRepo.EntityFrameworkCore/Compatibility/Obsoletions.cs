namespace RapidRepo.EntityFrameworkCore.Compatibility;

/// <summary>
/// Messages and the diagnostic ID shared by the 8.x type names kept for one major version to ease the upgrade.
/// </summary>
/// <remarks>
/// The shared <see cref="DiagnosticId"/> lets an application silence every shim warning at once
/// (<c>&lt;NoWarn&gt;RRMIG001&lt;/NoWarn&gt;</c>) while it migrates.
/// </remarks>
internal static class Obsoletions
{
    public const string DiagnosticId = "RRMIG001";

    private const string Suffix = " from RapidRepo.EntityFrameworkCore instead. This type will be removed in RapidRepo 10.0.";

    public const string Repository = "Use EfRepository<TEntity, TId>" + Suffix;
    public const string ContextBoundRepository = "Use EfRepository<TEntity, TId, TContext>" + Suffix;
    public const string BaseRepository = "Derive from EfRepository<TEntity, TId>" + Suffix;
    public const string ReadOnlyRepository = "Derive from EfReadOnlyRepository<TEntity, TId>" + Suffix;
    public const string WriteRepository = "Derive from EfWriteRepository<TEntity, TId>" + Suffix;
    public const string BaseWriteRepository = "Use EfRepository<TEntity, TId> or derive from EfWriteRepository<TEntity, TId>" + Suffix;
    public const string UnitOfWork = "Derive from EfUnitOfWork<TUserKey>" + Suffix;
}
