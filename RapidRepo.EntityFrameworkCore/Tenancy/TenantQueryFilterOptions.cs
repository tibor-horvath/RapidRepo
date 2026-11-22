namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// Options for <c>AddTenantQueryFilters</c>. They are stored in the model, so the tenancy validation applies them
/// wherever it runs.
/// </summary>
public sealed class TenantQueryFilterOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether a foreign key between two tenant entities that does not include
    /// <c>TenantId</c> fails the tenancy validation. Default: <see langword="false"/>, which reports it as a warning.
    /// </summary>
    /// <remarks>
    /// The unit of work refuses a commit that points such a key at another tenant's row, but writes that bypass it,
    /// such as raw SQL, are not checked. The composite-key pattern makes the database enforce it too: an alternate key
    /// <c>(TenantId, Id)</c> on the principal and a foreign key <c>(TenantId, PrincipalId)</c> on the dependent.
    /// </remarks>
    public bool RequireTenantScopedForeignKeys { get; set; }
}
