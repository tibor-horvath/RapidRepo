# Entities

All entities used with RapidRepo must inherit from one of the provided base classes.

---

## `BaseEntity<TKey>`

The simplest base class. Provides only an `Id` property. `TKey` must be a value type (e.g. `int`, `long`, `Guid`).

```csharp
public class Product : BaseEntity<long>
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

Extends `BaseAuditableEntity<TId>` and additionally tracks which user performed each operation. Both `TId` and `TUserKey` must be value types.

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
public class Product : BaseAuditableEntity<long, Guid>, IDeletableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
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
| Add soft delete to any of the above | Add `IDeletableEntity` or `IDeletableEntity<TUserKey>` |
