# Multi-Tenancy

Row-level tenant isolation: many tenants share one database and one schema, and every row of a tenant entity carries a `TenantId`. RapidRepo stamps it on insert, filters reads to the current tenant, and refuses commits that would touch another tenant's row. The rules are provider-neutral; each persistence provider documents how it filters (for EF Core, see [Tenant query filters](../RapidRepo.EntityFrameworkCore/entity-framework-core.md#tenant-query-filters)).

Database-per-tenant and schema-per-tenant are out of scope. So is working out the tenant from the request: that is your `ICurrentTenantProvider`.

---

## Design principles

1. **Fail closed.** No current tenant means an error, never unfiltered data. Cross-tenant access happens only inside an explicit scope.
2. **Trust the database, not the caller.** The tenant of an existing row is read from the store, never taken from the entity instance the caller passed in.
3. **Every write path is either guarded or documented as unguarded.** See [What is not guarded](#what-is-not-guarded).

---

## Making an entity a tenant entity

Implement `ITenantEntity<TTenantKey>`:

```csharp
using RapidRepo.Entities;              // BaseAuditableDeletableEntity
using RapidRepo.Entities.Interfaces;   // ITenantEntity<TTenantKey>

public class Invoice : BaseAuditableDeletableEntity<int, Guid>, ITenantEntity<Guid>
{
    public Guid TenantId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
}
```

The tenant key can be any type: a `Guid`, an `int`, or a `string` slug. Leave `TenantId` unset; the unit of work stamps it.

**What "no tenant" means.** `null`, or the default value of a value-type key (`Guid.Empty`, `0`). C# can't express a nullable `TTenantKey?` for a generic value type, so the default value doubles as "none". Don't use it as a real tenant. An empty string is never a tenant: a tenant provider that returns one, or an entity added with one, is an error.

In an inheritance hierarchy, implement the interface on the root type, as for [soft delete](soft-delete.md#inheritance).

---

## The current tenant

Implement `ICurrentTenantProvider<TTenantKey>` and register it with `AddRapidRepo`:

```csharp
using RapidRepo.Tenancy;   // ICurrentTenantProvider<TTenantKey>

public sealed class ClaimsTenantProvider(IHttpContextAccessor http) : ICurrentTenantProvider<Guid>
{
    public Guid GetCurrentTenantId() =>
        Guid.TryParse(http.HttpContext?.User.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;
}

builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<AppDbContext>();
    options.UseCurrentTenantProvider<ClaimsTenantProvider, Guid>();
    // or: options.UseCurrentTenant<Guid>(sp => /* read a claim or header */);
});
```

The tenant provider is asked on every read and once per commit, but never while a [tenant override](#acting-as-one-tenant) is open. With EF Core, the context also implements `ITenantDbContext<TTenantKey>` and delegates to the tenant provider; see [EF Core](../RapidRepo.EntityFrameworkCore/entity-framework-core.md#tenant-query-filters).

---

## Rules

### Reads

- Reads of tenant entities return only the current tenant's rows.
- With no current tenant (and no scope), reading a tenant entity throws a `TenantViolationException` instead of returning unfiltered rows.
- `ReadOptions.IncludeDeleted` lifts only the soft-delete filter, never the tenant filter.
- Lookups by id for writes (`DeleteById`, `RestoreById`, `HardDeleteById`) go through the tenant filter, so another tenant's id behaves like a missing id.

### Commits

The current tenant is resolved once per commit. Every tenant entity in the commit is checked, including entities reached through navigations, before anything is written:

| Change | Rule |
|---|---|
| Added | An unset `TenantId` gets the current tenant. A `TenantId` set to a different tenant throws. |
| Updated, soft-deleted, restored, hard-deleted | The tenant **stored in the database** must be the current tenant. Otherwise the commit throws. |
| `TenantId` changed | Throws: a row cannot move between tenants through the repository. |
| A foreign key set on add, or changed by an update, points at another tenant's row | Throws. This applies to any entity, including link tables that aren't tenant entities themselves. See [Relationships](#relationships). |
| No current tenant, no scope | Throws `TenantViolationException`. |
| Inside a host scope | The rules are skipped. An added entity must have its `TenantId` set. |

A commit that breaks a rule writes nothing.

**Why the stored tenant.** `Update(entity)` accepts an instance the repository never loaded, such as one built from a request body. Its `TenantId` proves nothing: a caller of tenant B can send `{ Id: <tenant A's id>, TenantId: B }`. So before writing, the unit of work reads the stored `TenantId` of every updated or deleted tenant entity, with one query per entity type, and compares that. This check is always on.

`TenantViolationException` is not a concurrency conflict, and retrying doesn't help. Map it to HTTP 403 or 404.

---

## Scopes

Code without a request tenant, such as a background job, seeding or a migration, opens an explicit scope. Scopes flow with `async` calls, nest, and restore the previous scope when disposed.

### Acting as one tenant

```csharp
using RapidRepo.Tenancy;   // ITenantScope<TTenantKey>

public sealed class InvoiceReminderJob(ITenantScope<Guid> tenantScope, IAppUnitOfWork unitOfWork)
{
    public async Task RunAsync(Guid tenantId)
    {
        using (tenantScope.Override(tenantId))
        {
            var unpaid = await unitOfWork.Invoices.GetAllAsync(i => i.PaidAt == null);
            // reads and commits act as tenantId
        }
    }
}
```

An override takes precedence over the tenant provider and narrows access to a single tenant. While it is open the tenant provider is not asked at all, so a tenant provider that reads the current request (a claim, a header) may assume there is one.

### Acting across tenants

```csharp
using RapidRepo.Tenancy.Host;   // ITenantHostScope

public sealed class TenantReportJob(ITenantHostScope hostScope, IAppUnitOfWork unitOfWork)
{
    public async Task<int> CountInvoicesAsync()
    {
        using (hostScope.BeginHostScope())
        {
            // every tenant's rows; commits skip the tenant rules
            return await unitOfWork.Invoices.CountAsync();
        }
    }
}
```

`ITenantHostScope` lives in its own namespace, `RapidRepo.Tenancy.Host`, so request-handling code can be kept from using it with an analyzer or an architecture test. Every host scope:

- is logged at Information level with its caller (file, member, line),
- tags the current `Activity` with `rapidrepo.tenant.host_scope = true`, so host access shows up in traces,
- is checked against an `ITenantHostScopePolicy`, if you register one. Without one, host scopes are allowed.

```csharp
// Deny host scopes inside HTTP requests.
public sealed class NoHostScopeInRequests(IHttpContextAccessor http) : ITenantHostScopePolicy
{
    public bool IsAllowed(TenantHostScopeRequest request) => http.HttpContext is null;
}

builder.Services.AddSingleton<ITenantHostScopePolicy, NoHostScopeInRequests>();
```

Without dependency injection, create the scopes directly: `new TenantScope<Guid>()` and `new TenantHostScope(policy)`. These don't log.

> **Coming from ABP?** ABP treats "no tenant" as the host and returns host-owned rows without an error. RapidRepo fails closed instead: "no tenant" is an error, and the host is only ever an explicit scope.

---

## Relationships

A commit refuses a foreign key that points at another tenant's row. Without it, a request body such as `{ "invoiceId": 42 }` could link the caller's row to another tenant's invoice: the database would accept it, because the invoice exists.

The check covers:

- every foreign key of an **added** entity, and every foreign key an **update** changes (an `Update` of an untracked instance changes them all);
- entities that are not tenant entities themselves, such as a link table between two tenant entities. Without a current tenant, a commit that sets such a reference throws, like any other tenant write;
- rows of the other tenant that are soft-deleted.

It skips references to rows added in the same commit, which get the current tenant anyway, foreign keys that include `TenantId`, and everything inside a host scope. The error doesn't say whether the referenced row exists, so map it to the same response as a missing reference (usually 400 or 404).

The check costs one query per referenced entity type, and only in commits that set or change such a reference. To have the database enforce the rule as well, including for writes that bypass RapidRepo, use the composite-key pattern; the commit then skips the check for that key:

```csharp
modelBuilder.Entity<Invoice>().HasAlternateKey(i => new { i.TenantId, i.Id });
modelBuilder.Entity<InvoiceLine>()
    .HasOne<Invoice>().WithMany()
    .HasForeignKey(l => new { l.TenantId, l.InvoiceId })
    .HasPrincipalKey(i => new { i.TenantId, i.Id });
```

The EF Core validation warns about every foreign key between tenant entities that doesn't include `TenantId` (ownerships aside: an owned type always belongs to its owner's tenant). To make the warning an error, use `AddTenantQueryFilters(this, o => o.RequireTenantScopedForeignKeys = true)`.

---

## What is not guarded

- **Raw SQL** (`FromSql`, `ExecuteSql`) and any other client of the database.
- **Bulk updates.** With EF Core, `ExecuteUpdate` and `ExecuteDelete` stay scoped to the current tenant, because they honour query filters. They bypass stamping and the `TenantId` guard, so never set `TenantId` in them.
- **Unique indexes.** With a global unique index, a unique-violation error tells tenant B that tenant A's value exists. Include `TenantId` in unique indexes.

For enforcement that also covers these, add database row-level security (SQL Server RLS with `SESSION_CONTEXT`, PostgreSQL RLS with `current_setting`) as defence in depth.

---

## Validation

A misconfiguration fails when the app starts, not the first time a code path touches an entity. When a tenant provider is configured, `AddRapidRepo` runs the persistence provider's tenancy validation (for EF Core, the model checks) in a hosted service, so `host.StartAsync()` fails. Apps without a host, and tests, call:

```csharp
serviceProvider.ValidateRapidRepoTenancy();
```

A failure throws one `RapidRepoConfigurationException` listing every problem, not only the first. Read its `Problems` in a test or a health check rather than parsing the message:

```csharp
try
{
    serviceProvider.ValidateRapidRepoTenancy();
}
catch (RapidRepoConfigurationException exception)
{
    foreach (var problem in exception.Problems)
        logger.LogCritical("{Problem}", problem);
    throw;
}
```

The validation also runs on first use, the first time a tenant entity of a model is read or committed. `options.ValidateTenancyOnStartup = false` turns off only the hosted service. For the EF Core checks, see [Tenancy validation](../RapidRepo.EntityFrameworkCore/entity-framework-core.md#tenancy-validation).

---

## Migrating a hand-written tenant filter

Applications that already have a `TenantId` and their own `HasQueryFilter(e => e.TenantId == ...)`:

1. Implement `ITenantEntity<TTenantKey>`. The existing `TenantId` property satisfies it.
2. Remove the tenant condition from the existing filter. If the filter has other conditions, make it a named filter (`HasQueryFilter("MyFilter", ...)`). `AddTenantQueryFilters` refuses to replace an anonymous filter.
3. Implement `ITenantDbContext<TTenantKey>` on the context, call `modelBuilder.AddTenantQueryFilters(this)`, and register a tenant provider.
4. Wrap background jobs, seeding and admin paths that run without a tenant in `Override(...)` or `BeginHostScope()`. **Until you do, they throw.**
5. Remove manual `TenantId` assignment before save. It still works, as long as it matches the current tenant.
