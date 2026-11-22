using Microsoft.EntityFrameworkCore.Metadata;
using RapidRepo.Tenancy;

namespace RapidRepo.EntityFrameworkCore.Internal;

/// <summary>
/// Which foreign keys can point across tenants, shared by the validation (which reports them) and the commit (which
/// checks them), so the two always agree.
/// </summary>
internal static class TenantForeignKeys
{
    /// <summary>
    /// Gets a value indicating whether <paramref name="foreignKey"/> refers to a tenant entity without including
    /// <c>TenantId</c>, so the database alone does not keep it within one tenant. Ownerships are left out: an owned
    /// entity always shares its owner's row or tenant.
    /// </summary>
    public static bool IsUnscoped(IReadOnlyForeignKey foreignKey) =>
        !foreignKey.IsOwnership
        && TenantTraits.IsTenantEntity(foreignKey.PrincipalEntityType.ClrType)
        && !foreignKey.Properties.Any(p => p.Name == TenantTraits.TenantIdPropertyName);
}
