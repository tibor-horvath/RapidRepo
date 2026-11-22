using RapidRepo.Tenancy;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// What a provider that supports multi-tenancy supplies to run <see cref="TenantConformanceTests{TStore}"/>: a store
/// that also holds <see cref="Invoice"/> and <see cref="Payment"/>, isolated by tenant the way the provider normally
/// sets that up (for EF Core, <c>AddTenantQueryFilters(this)</c> on a context that implements
/// <c>ITenantDbContext&lt;Guid&gt;</c>). <see cref="Payment.InvoiceId"/> is mapped as a foreign key to
/// <see cref="Invoice"/>.
/// </summary>
/// <remarks>
/// Separate from <see cref="IConformanceStore"/>, so providers without tenancy support keep compiling and passing.
/// </remarks>
public interface ITenantConformanceStore : IConformanceStore
{
    /// <summary>
    /// Opens a session whose current tenant comes from <paramref name="tenantProvider"/>.
    /// </summary>
    /// <param name="clock">The clock the unit of work stamps audit fields with.</param>
    /// <param name="tenantProvider">Supplies the current tenant, the way the request would.</param>
    /// <returns>The session.</returns>
    IConformanceSession OpenSession(TimeProvider clock, ICurrentTenantProvider<Guid> tenantProvider);
}
