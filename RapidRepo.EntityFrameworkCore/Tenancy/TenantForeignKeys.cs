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
    /// Gets a value indicating whether <paramref name="foreignKey"/> refers to a tenant entity without being kept
    /// within the current tenant by the database. That holds only for a <em>tenant-entity</em> dependent that maps its
    /// <c>TenantId</c> to the principal's <c>TenantId</c>: the commit stamps and checks the dependent's <c>TenantId</c>,
    /// so the database then keeps the reference within the current tenant. The mapping is positional: at the position
    /// of the dependent's <c>TenantId</c>, the principal key must have <c>TenantId</c> too. A dependent that is not a
    /// tenant entity is never exempt, even with a composite key that includes <c>TenantId</c>: nothing stamps or checks
    /// its <c>TenantId</c>, so the caller chooses it, and the database keeps the reference within <em>that</em> tenant,
    /// not the current one. Ownerships are left out: an owned entity always shares its owner's row or tenant.
    /// </summary>
    public static bool IsUnscoped(IReadOnlyForeignKey foreignKey) =>
        !foreignKey.IsOwnership
        && TenantTraits.IsTenantEntity(foreignKey.PrincipalEntityType.ClrType)
        && !(TenantTraits.IsTenantEntity(foreignKey.DeclaringEntityType.ClrType) && MapsTenantIdToTenantId(foreignKey));

    private static bool MapsTenantIdToTenantId(IReadOnlyForeignKey foreignKey)
    {
        var principalProperties = foreignKey.PrincipalKey.Properties;

        for (var i = 0; i < foreignKey.Properties.Count; i++)
        {
            if (foreignKey.Properties[i].Name == TenantTraits.TenantIdPropertyName
                && principalProperties[i].Name == TenantTraits.TenantIdPropertyName)
            {
                return true;
            }
        }

        return false;
    }
}
