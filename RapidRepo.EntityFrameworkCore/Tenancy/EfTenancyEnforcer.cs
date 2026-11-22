using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RapidRepo.Tenancy;

namespace RapidRepo.EntityFrameworkCore.Internal;

/// <summary>
/// Applies the tenant rules to a context's staged changes before anything is written: sorts the change tracker's
/// entries, then hands each kind of check to the guard with the facts it needs from the database.
/// </summary>
internal static class EfTenancyEnforcer
{
    /// <summary>
    /// Applies the tenant rules to every staged tenant entity, including those reached through navigations: stamps
    /// added entities, checks updated and deleted ones against the tenant stored in the database, and refuses foreign
    /// keys, set on add or changed by an update, that point at another tenant's row. Does nothing when no tenant
    /// entity is staged and no staged entity refers to one.
    /// </summary>
    /// <param name="context">The context about to be saved.</param>
    /// <param name="createGuard">Creates the commit's guard; only called when there is something to check.</param>
    /// <param name="async">Whether to query the database asynchronously. When <see langword="false"/>, the returned task is already complete.</param>
    /// <param name="cancellationToken">Cancels the database queries.</param>
    /// <exception cref="TenantViolationException">A rule is broken; nothing is written.</exception>
    public static async Task EnforceAsync(
        DbContext context,
        Func<TenantCommitGuard> createGuard,
        bool async,
        CancellationToken cancellationToken)
    {
        var changed = context.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        var tenantEntries = changed.Where(e => TenantTraits.IsTenantEntity(e.Entity.GetType())).ToList();

        // Entries of any type, link tables included, that may refer to a tenant entity through a foreign key.
        var referencing = changed
            .Where(e => e.State != EntityState.Deleted && TenantReferenceVerifier.HasTenantReferences(e.Metadata))
            .ToList();

        if (tenantEntries.Count == 0 && referencing.Count == 0)
            return;

        TenancyValidator.EnsureValid(context);

        var guard = createGuard();

        foreach (var entry in tenantEntries.Where(e => e.State == EntityState.Added))
            guard.ApplyToAdded(entry.Entity);

        var existing = tenantEntries.Where(e => e.State != EntityState.Added).ToList();

        if (guard.IsHostScope)
        {
            await PinStoredTenantsAsync(context, existing, async, cancellationToken);
            return;
        }

        await VerifyExistingAsync(
            context,
            guard,
            existing,
            async,
            cancellationToken);

        await TenantReferenceVerifier.VerifyAsync(
            context,
            guard,
            referencing,
            changed.Where(e => e.State == EntityState.Added).ToList(),
            async,
            cancellationToken);
    }

    /// <summary>
    /// Checks updated and deleted tenant entities against the tenant stored in their rows.
    /// </summary>
    private static async Task VerifyExistingAsync(
        DbContext context,
        TenantCommitGuard guard,
        IReadOnlyList<EntityEntry> existing,
        bool async,
        CancellationToken cancellationToken)
    {
        if (existing.Count == 0)
            return;

        // Fail fast without a tenant, before querying the database. Every tenant entity of a context shares one key
        // type (the validation checks it), so this is the tenant of every entry.
        var current = guard.RequireCurrentTenant(existing[0].Entity.GetType());

        // Only rows of other tenants come back: the database matches the keys with its own collation, the way the
        // UPDATE or DELETE will. An entry left out has a row of the current tenant, or none.
        var foreign = await StoredTenantQuery.LoadOtherTenantsAsync(context, existing, current, async, cancellationToken);

        foreach (var entry in existing)
        {
            var kind = entry.State == EntityState.Deleted ? TenantWriteKind.Delete : TenantWriteKind.Update;

            if (foreign.TryGetValue(entry, out var storedTenantId))
                guard.VerifyExisting(entry.Entity, kind, storedTenantId);
            else
                guard.VerifyExisting(entry.Entity, kind);
        }

        // TenantId is a concurrency token, so the UPDATE or DELETE matches the row only while it still belongs to the
        // tenant just checked: a row moved to another tenant since then is not written.
        foreach (var entry in existing)
            PinTenant(entry, current);
    }

    /// <summary>
    /// In a host scope nothing is checked, but each update and delete is still pinned to the tenant stored in its row,
    /// read now, so a row moved by someone else before the write is not written. Taking the instance's
    /// <c>TenantId</c> instead would break moving a row on purpose, and deleting an instance built with only its key.
    /// </summary>
    private static async Task PinStoredTenantsAsync(
        DbContext context,
        IReadOnlyList<EntityEntry> existing,
        bool async,
        CancellationToken cancellationToken)
    {
        if (existing.Count == 0)
            return;

        var stored = await StoredTenantQuery.LoadOtherTenantsAsync(context, existing, currentTenant: null, async, cancellationToken);

        foreach (var (entry, tenantId) in stored)
        {
            if (tenantId is not null)
                PinTenant(entry, tenantId);
        }
    }

    private static void PinTenant(EntityEntry entry, object tenantId) =>
        entry.Property(TenantTraits.TenantIdPropertyName).OriginalValue = tenantId;
}
