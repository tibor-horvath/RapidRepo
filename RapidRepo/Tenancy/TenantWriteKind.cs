namespace RapidRepo.Tenancy;

/// <summary>
/// The kind of write to an existing tenant entity that <see cref="TenantCommitGuard.VerifyExisting(object, TenantWriteKind)"/> checks.
/// </summary>
public enum TenantWriteKind
{
    /// <summary>An update, including a soft delete and a restore.</summary>
    Update,

    /// <summary>A hard delete.</summary>
    Delete,
}
