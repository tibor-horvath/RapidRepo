using RapidRepo.Tenancy;

namespace RapidRepo.Testing.Conformance;

/// <summary>A tenant provider that returns a fixed tenant.</summary>
/// <param name="tenantId">The tenant, or <see langword="null"/> (here <see cref="Guid.Empty"/>) for none.</param>
public sealed class ConformanceTenant(Guid? tenantId) : ICurrentTenantProvider<Guid>
{
    /// <inheritdoc />
    public Guid GetCurrentTenantId() => tenantId ?? Guid.Empty;
}
