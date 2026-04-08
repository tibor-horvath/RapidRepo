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

## Soft Delete (`IDeletableEntity`)

Implement `IDeletableEntity` or `IDeletableEntity<TUserKey>` on any entity to enable soft delete. The `DeletedAt` (and optionally `DeletedBy`) fields are set automatically by the Unit of Work when the entity is deleted.

```csharp
// Timestamp only
public class Product : BaseAuditableEntity<long>, IDeletableEntity
{
    public string Name { get; set; } = string.Empty;
}

// With user tracking
// BaseAuditableEntity<TId, TUserKey> has no DeletedAt, so both interface members must be declared.
public class Product : BaseAuditableEntity<long, Guid>, IDeletableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public DateTime? DeletedAt { get; set; }  // required by IDeletableEntity
    public Guid? DeletedBy { get; set; }       // required by IDeletableEntity<Guid>
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
| Add soft delete to any of the above | Add `IDeletableEntity` or `IDeletableEntity<TUserKey>` |
