namespace RapidRepo.Entities.Interfaces;

/// <summary>
/// Represents an entity that belongs to one tenant. Reads return only the current tenant's rows, a new entity is
/// stamped with the current tenant, and a commit refuses to write a row of another tenant.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TenantId"/> is managed by the library on insert: leave it unset (<see langword="null"/>, or the
/// default value for a value-type key such as <see cref="Guid.Empty"/>) and the unit of work stamps the current
/// tenant. Setting it to a different tenant than the current one makes the commit throw. An empty string is never a
/// valid tenant.
/// </para>
/// <para>
/// A row cannot move between tenants through the repository: a commit that changes <see cref="TenantId"/> of an
/// existing entity throws. Nor can a row refer to another tenant's row: a commit that sets a foreign key to a row of
/// another tenant throws, whether or not the referring entity is a tenant entity itself.
/// </para>
/// <para>
/// The tenant of the current request comes from the provider (for EF Core, <c>ITenantDbContext&lt;TTenantKey&gt;</c>),
/// and can be replaced for a block of code with <see cref="Tenancy.ITenantScope{TTenantKey}.Override"/>.
/// </para>
/// </remarks>
/// <typeparam name="TTenantKey">The type of the tenant key, for example <see cref="Guid"/> or <see cref="string"/>.</typeparam>
public interface ITenantEntity<TTenantKey>
    where TTenantKey : notnull
{
    /// <summary>
    /// Gets or sets the tenant the entity belongs to.
    /// </summary>
    TTenantKey TenantId { get; set; }
}
