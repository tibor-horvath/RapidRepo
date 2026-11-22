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
    /// Gets a value indicating whether <paramref name="foreignKey"/> refers to a tenant entity without mapping the
    /// dependent's <c>TenantId</c> to the principal's <c>TenantId</c>, so the database alone does not keep it within one
    /// tenant. The mapping is positional: the foreign key is scoped only when, at the position of the dependent's
    /// <c>TenantId</c>, the principal key has <c>TenantId</c> too. A dependent <c>TenantId</c> that maps to another
    /// principal column scopes nothing. Ownerships are left out: an owned entity always shares its owner's row or
    /// tenant.
    /// </summary>
    public static bool IsUnscoped(IReadOnlyForeignKey foreignKey) =>
        !foreignKey.IsOwnership
        && TenantTraits.IsTenantEntity(foreignKey.PrincipalEntityType.ClrType)
        && !MapsTenantIdToTenantId(foreignKey);

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
