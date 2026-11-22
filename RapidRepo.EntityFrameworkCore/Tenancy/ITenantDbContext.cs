namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// A <c>DbContext</c> that knows the tenant of the current request, so that the tenant query filter registered by
/// <c>AddTenantQueryFilters</c> and the unit of work can read it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CurrentTenantId"/> is read on every query and every commit, never cached when the context is built,
/// so the usual implementation delegates to a tenant provider:
/// </para>
/// <code>
/// public sealed class AppDbContext(DbContextOptions&lt;AppDbContext&gt; options, ICurrentTenantProvider&lt;Guid&gt; tenants)
///     : DbContext(options), ITenantDbContext&lt;Guid&gt;
/// {
///     public Guid CurrentTenantId =&gt; tenants.GetCurrentTenantId();
/// }
/// </code>
/// <para>
/// Constructing the context never resolves the tenant, so design-time tools such as <c>dotnet ef migrations</c> need
/// none.
/// </para>
/// <para>
/// With context pooling (<c>AddDbContextPool</c>), a context outlives the scope it was leased in and cannot take
/// scoped services. Read the tenant from ambient state instead, for example through a singleton provider over
/// <c>IHttpContextAccessor</c>.
/// </para>
/// </remarks>
/// <typeparam name="TTenantKey">The tenant key type, the same as the entities' <c>ITenantEntity&lt;TTenantKey&gt;</c>.</typeparam>
public interface ITenantDbContext<TTenantKey>
    where TTenantKey : notnull
{
    /// <summary>
    /// Gets the tenant of the current request: <see langword="null"/>, or the default value for a value-type key
    /// (for example <see cref="Guid.Empty"/>), when there is none.
    /// </summary>
    TTenantKey? CurrentTenantId { get; }
}
