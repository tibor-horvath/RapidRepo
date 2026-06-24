# Entities

All entities used with RapidRepo must inherit from one of the provided base classes.

---

## `BaseEntity<TKey>`

The simplest base class. Provides only an `Id` property. `TKey` can be any non-null key type (for example `int`, `long`, `Guid`, or `string`).

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

Extends `BaseEntity<TId>` with timestamp-only audit fields. The Unit of Work automatically sets these on `Commit`/`CommitAsync`.

| Property | Type | Set on |
|---|---|---|
| `CreatedAt` | `DateTime` | Insert |
| `ModifiedAt` | `DateTime?` | Update |
| `DeletedAt` | `DateTime?` | Soft delete |

```csharp
public class Product : BaseAuditableEntity<long>
{
    public string Name { get; set; } = string.Empty;
}
```

---

## `BaseAuditableEntity<TId, TUserKey>`

Inherits directly from `BaseEntity<TId>` and implements `IAuditableEntity<TUserKey>` to track which user performed each operation. `TId` can be any non-null key type, while `TUserKey` must be a value type. Note: this variant does **not** include a `DeletedAt` property — see [Soft Delete](#soft-delete-ideletableentity) below.

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

When you need soft-delete on an entity that does not already inherit from `BaseAuditableDeletableEntity`, implement `IDeletableEntity` or `IDeletableEntity<TUserKey>` directly and declare the required properties. The `DeletedAt` (and optionally `DeletedBy`) fields are set automatically by the Unit of Work when the entity is deleted.

```csharp
// Timestamp only — declare DeletedAt to satisfy IDeletableEntity
public class Product : BaseAuditableEntity<long>, IDeletableEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime? DeletedAt { get; set; }
}

// With user tracking — declare both DeletedAt and DeletedBy
public class Product : BaseAuditableEntity<long, Guid>, IDeletableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
```

> To hide soft-deleted records automatically, configure a global EF Core query filter on `DeletedAt == null` in your `DbContext`. Use `ignoreQueryFilters: true` on any query to bypass it.

---

## Choosing the right base class

| Scenario | Base class |
|---|---|
| No audit requirements | `BaseEntity<TId>` |
| Track created/modified timestamps | `BaseAuditableEntity<TId>` |
| Track who created/modified the record | `BaseAuditableEntity<TId, TUserKey>` |
| Track timestamps + soft delete | `BaseAuditableDeletableEntity<TId>` |
| Track who created/modified + soft delete | `BaseAuditableDeletableEntity<TId, TUserKey>` |
| Add soft delete to an existing hierarchy | Add `IDeletableEntity` or `IDeletableEntity<TUserKey>` |

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

Combined with `class AppUnitOfWork : UnitOfWork<Guid>` (or `UnitOfWork<long>`), the user-key
type is declared in exactly two places across the whole application.
