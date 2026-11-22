# Entities

All entities used with RapidRepo must inherit from `BaseEntity<TId>`, directly or through one of the other base classes on this page. Optional features, such as soft delete, concurrency and multi-tenancy, are added through interfaces.

`TId` is the entity's key type. The repository interfaces call the same type `TKey`, so `IRepository<Product, long>` is the repository of a `Product : BaseEntity<long>`.

---

## `BaseEntity<TId>`

The simplest base class. Provides only an `Id` property. `TId` can be any non-null key type (for example `int`, `long`, `Guid`, or `string`).

```csharp
public class Product : BaseEntity<string>
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

---

## `BaseAuditableEntity<TId>`

Extends `BaseEntity<TId>` with timestamp-only audit fields. The Unit of Work sets these on `Commit`/`CommitAsync`; see [Auditing](auditing.md).

| Property | Type | Set on |
|---|---|---|
| `CreatedAt` | `DateTime` | Insert |
| `ModifiedAt` | `DateTime?` | Update |

This variant has no `DeletedAt`. For soft delete, use `BaseAuditableDeletableEntity<TId>` or add `IDeletableEntity`; see [Soft Delete](soft-delete.md).

```csharp
public class Product : BaseAuditableEntity<long>
{
    public string Name { get; set; } = string.Empty;
}
```

---

## `BaseAuditableEntity<TId, TUserKey>`

Inherits directly from `BaseEntity<TId>` and implements `IAuditableEntity<TUserKey>` to track which user performed each operation. `TId` can be any non-null key type, while `TUserKey` must be a value type. Note: this variant does **not** include a `DeletedAt` property — see [Soft Delete](soft-delete.md).

| Property | Type | Set on |
|---|---|---|
| `CreatedAt` | `DateTime` | Insert |
| `CreatedBy` | `TUserKey` | Insert |
| `ModifiedAt` | `DateTime?` | Update |
| `ModifiedBy` | `TUserKey?` | Update |

```csharp
public class Product : BaseAuditableEntity<long, Guid>
{
    public string Name { get; set; } = string.Empty;
}
```

---

## `BaseAuditableDeletableEntity<TId>` / `BaseAuditableDeletableEntity<TId, TUserKey>`

A ready-made base class that combines full audit fields and soft-delete support. Prefer this over manually combining `BaseAuditableEntity` with `IDeletableEntity`.

`BaseAuditableDeletableEntity<TId>` — timestamp-only audit with soft delete:

| Property | Type | Set on |
|---|---|---|
| `CreatedAt` | `DateTime` | Insert |
| `ModifiedAt` | `DateTime?` | Update |
| `DeletedAt` | `DateTime?` | Soft delete |

```csharp
public class Product : BaseAuditableDeletableEntity<long>
{
    public string Name { get; set; } = string.Empty;
}
```

`BaseAuditableDeletableEntity<TId, TUserKey>` — full audit with user tracking and soft delete:

| Property | Type | Set on |
|---|---|---|
| `CreatedAt` | `DateTime` | Insert |
| `CreatedBy` | `TUserKey` | Insert |
| `ModifiedAt` | `DateTime?` | Update |
| `ModifiedBy` | `TUserKey?` | Update |
| `DeletedAt` | `DateTime?` | Soft delete |
| `DeletedBy` | `TUserKey?` | Soft delete |

```csharp
public class Product : BaseAuditableDeletableEntity<long, Guid>
{
    public string Name { get; set; } = string.Empty;
}
```

---

## Soft Delete (`IDeletableEntity`)

To add soft delete to an entity that does not inherit from `BaseAuditableDeletableEntity`, implement `IDeletableEntity` or `IDeletableEntity<TUserKey>` and declare `DeletedAt` (and `DeletedBy`). In an inheritance hierarchy, implement it on the root type. See [Soft Delete](soft-delete.md) for the setup, reads, restore and permanent removal.

```csharp
public class Product : BaseAuditableEntity<long>, IDeletableEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime? DeletedAt { get; set; }
}
```

---

## Optimistic concurrency (`IVersionedEntity`)

Implement `IVersionedEntity`, or inherit from `BaseVersionedEntity<TId>`, to make a commit fail with `ConcurrencyException` instead of overwriting a row someone else changed since it was read. See [Optimistic Concurrency](concurrency.md).

```csharp
public class Order : BaseAuditableEntity<int, Guid>, IVersionedEntity
{
    public string Status { get; set; } = "";
    public long Version { get; set; }
}
```

---

## Multi-tenancy (`ITenantEntity<TTenantKey>`)

Implement `ITenantEntity<TTenantKey>` to give each row a tenant: reads see only the current tenant's rows, and a commit stamps new rows and refuses to touch another tenant's. See [Multi-Tenancy](multi-tenancy.md).

```csharp
public class Invoice : BaseAuditableDeletableEntity<int, Guid>, ITenantEntity<Guid>
{
    public Guid TenantId { get; set; }
    public string Number { get; set; } = "";
}
```

---

## Choosing the right base class

| Scenario | Base class |
|---|---|
| No audit requirements | `BaseEntity<TId>` |
| Track created/modified timestamps | `BaseAuditableEntity<TId>` |
| Track who created/modified the record | `BaseAuditableEntity<TId, TUserKey>` |
| Track timestamps + soft delete | `BaseAuditableDeletableEntity<TId>` |
| Track who created/modified + soft delete | `BaseAuditableDeletableEntity<TId, TUserKey>` |
| Add soft delete to an existing hierarchy | Add `IDeletableEntity` or `IDeletableEntity<TUserKey>` to the hierarchy's root type |
| Detect concurrent edits | `BaseVersionedEntity<TId>`, or add `IVersionedEntity` to any of the above |
| Isolate rows per tenant | Add `ITenantEntity<TTenantKey>` to any of the above (on the hierarchy's root type) |

---

## Reducing repetition with application-level base classes

When all entities in your application use the same user-key type, you can define it once by
creating thin abstract base classes that lock in `TUserKey`:

```csharp
// Guid-keyed users
public abstract class AppAuditableEntity<TId> : BaseAuditableEntity<TId, Guid>
    where TId : notnull { }

public abstract class AppAuditableDeletableEntity<TId> : BaseAuditableDeletableEntity<TId, Guid>
    where TId : notnull { }
```

```csharp
// long-keyed users (e.g. database integer IDs)
public abstract class AppAuditableEntity<TId> : BaseAuditableEntity<TId, long>
    where TId : notnull { }

public abstract class AppAuditableDeletableEntity<TId> : BaseAuditableDeletableEntity<TId, long>
    where TId : notnull { }
```

Each entity then only needs to declare its primary-key type — the user-key type is inherited:

```csharp
public class Employee : AppAuditableEntity<int>  { }
public class Product  : AppAuditableEntity<long> { }
public class AuditLog : AppAuditableDeletableEntity<int> { }
```

Combined with `class AppUnitOfWork : EfUnitOfWork<Guid>` (or `EfUnitOfWork<long>`), the user-key
type is declared in exactly two places across the whole application.
