namespace RapidRepo.Tenancy;

/// <summary>
/// Applies the tenant rules to the changes of one commit, so they live in one place whatever the persistence
/// technology. A provider gets one from <c>UnitOfWorkBase&lt;TUserKey&gt;.CreateTenantGuard()</c> and runs it over
/// every staged tenant entity before it writes anything.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>
/// An added entity whose <c>TenantId</c> is unset gets the current tenant; one set to a different tenant throws.
/// </item>
/// <item>
/// An updated or deleted entity is checked against the tenant <em>stored</em> in the database, which the provider
/// reads before the commit. The entity instance proves nothing: one built from a request body can carry any
/// <c>TenantId</c>.
/// </item>
/// <item>An update that changes <c>TenantId</c> throws: a row cannot move between tenants.</item>
/// <item>
/// An added entity, or an update that changes a foreign key, cannot refer to a row of a tenant entity that belongs to
/// another tenant (<see cref="VerifyReference"/>). This holds for entities that are not tenant entities themselves,
/// such as link tables.
/// </item>
/// <item>
/// Without a current tenant, any staged tenant entity makes the commit throw, unless the commit runs in a host
/// scope, which skips the rules (an entity added there must have its <c>TenantId</c> set).
/// </item>
/// </list>
/// The current tenant is resolved at most once per key type for the whole commit.
/// </remarks>
public sealed class TenantCommitGuard
{
    private readonly Func<Type, object?> _resolveTenant;
    private readonly Dictionary<Type, object?> _tenants = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantCommitGuard"/> class.
    /// </summary>
    /// <param name="isHostScope">Whether the commit runs in a host scope.</param>
    /// <param name="resolveTenant">
    /// Returns the current tenant for a tenant key type, or <see langword="null"/> when there is none.
    /// </param>
    public TenantCommitGuard(bool isHostScope, Func<Type, object?> resolveTenant)
    {
        ArgumentNullException.ThrowIfNull(resolveTenant);

        IsHostScope = isHostScope;
        _resolveTenant = resolveTenant;
    }

    /// <summary>
    /// Gets a value indicating whether the commit runs in a host scope, which skips the tenant rules.
    /// </summary>
    public bool IsHostScope { get; }

    /// <summary>
    /// Gets the current tenant for <paramref name="entityType"/>, or throws when there is none.
    /// </summary>
    /// <param name="entityType">A tenant entity type.</param>
    /// <returns>The current tenant, boxed.</returns>
    /// <exception cref="TenantViolationException">There is no current tenant.</exception>
    public object RequireCurrentTenant(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        var keyType = TenantTraits.GetTenantKeyType(entityType)
            ?? throw new ArgumentException($"'{entityType.Name}' is not a tenant entity.", nameof(entityType));

        if (!_tenants.TryGetValue(keyType, out var tenant))
        {
            tenant = _resolveTenant(keyType);
            _tenants[keyType] = tenant;
        }

        return tenant ?? throw TenantViolationException.MissingTenant(entityType, "commit");
    }

    /// <summary>
    /// Stamps an added tenant entity with the current tenant, or checks the tenant it already has.
    /// </summary>
    /// <param name="entity">The added entity.</param>
    /// <exception cref="TenantViolationException">
    /// There is no current tenant; the entity belongs to another tenant; its <c>TenantId</c> is an empty string; or,
    /// in a host scope, its <c>TenantId</c> is unset.
    /// </exception>
    public void ApplyToAdded(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var entityType = entity.GetType();
        var tenantId = TenantTraits.GetTenantId(entity);

        if (tenantId is string { Length: 0 })
        {
            throw new TenantViolationException(
                $"The added {entityType.Name} has an empty string as its TenantId. An empty string is not a tenant: " +
                "leave TenantId null to have the current tenant stamped.",
                entityType);
        }

        if (IsHostScope)
        {
            if (TenantKeys.IsNone(tenantId))
            {
                throw new TenantViolationException(
                    $"An {entityType.Name} was added in a host scope without a TenantId. A host scope has no tenant " +
                    "to stamp, so set TenantId explicitly, or add it inside a tenant override instead.",
                    entityType);
            }

            return;
        }

        var current = RequireCurrentTenant(entityType);

        if (TenantKeys.IsNone(tenantId))
        {
            TenantTraits.SetTenantId(entity, current);
            return;
        }

        if (!Equals(tenantId, current))
        {
            throw new TenantViolationException(
                $"An {entityType.Name} was added for a different tenant than the current one. Leave TenantId unset " +
                "to have the current tenant stamped, or add it inside a tenant override for its tenant.",
                entityType);
        }
    }

    /// <summary>
    /// Checks an update or delete of an existing tenant entity whose row was found in the database, against the
    /// tenant stored in that row.
    /// </summary>
    /// <param name="entity">The entity being updated or deleted.</param>
    /// <param name="kind">Whether it is an update or a delete.</param>
    /// <param name="storedTenantId">The tenant stored in the entity's row.</param>
    /// <exception cref="TenantViolationException">
    /// There is no current tenant; the stored row belongs to another tenant; or an update changes <c>TenantId</c>.
    /// </exception>
    public void VerifyExisting(object entity, TenantWriteKind kind, object? storedTenantId)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (IsHostScope)
            return;

        var entityType = entity.GetType();

        if (!Equals(storedTenantId, RequireCurrentTenant(entityType)))
        {
            throw new TenantViolationException(
                $"Cannot {DescribeAction(kind)} the {entityType.Name}: its row belongs to another tenant. Nothing was written.",
                entityType);
        }

        VerifyExisting(entity, kind);
    }

    /// <summary>
    /// Checks an update or delete of an existing tenant entity whose row the provider did not find for another
    /// tenant: either it belongs to the current tenant, or it does not exist.
    /// </summary>
    /// <param name="entity">The entity being updated or deleted.</param>
    /// <param name="kind">Whether it is an update or a delete.</param>
    /// <exception cref="TenantViolationException">
    /// There is no current tenant, or an update changes <c>TenantId</c>.
    /// </exception>
    /// <remarks>
    /// A row that does not exist is let through: it cannot belong to another tenant, and the write affects nothing,
    /// which the provider reports the way it always does (for EF Core, a concurrency error).
    /// </remarks>
    public void VerifyExisting(object entity, TenantWriteKind kind)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (IsHostScope)
            return;

        var entityType = entity.GetType();
        var current = RequireCurrentTenant(entityType);

        if (kind == TenantWriteKind.Update && !Equals(TenantTraits.GetTenantId(entity), current))
        {
            throw new TenantViolationException(
                $"Cannot update the {entityType.Name}: its TenantId was changed. A row cannot move between tenants " +
                "through the repository. Nothing was written.",
                entityType);
        }
    }

    /// <summary>
    /// Checks a reference from an added or updated entity to a row of a tenant entity, against the tenant stored in
    /// that row.
    /// </summary>
    /// <param name="entity">
    /// The entity that holds the reference. It need not be a tenant entity itself: a link table that joins two tenant
    /// entities is checked the same way.
    /// </param>
    /// <param name="principalType">The tenant entity type the reference points at.</param>
    /// <param name="foreignKeyProperties">The names of the properties that hold the reference, for the message.</param>
    /// <param name="principalTenantId">The tenant stored in the referenced row.</param>
    /// <exception cref="TenantViolationException">
    /// There is no current tenant, or the referenced row belongs to another tenant.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The provider calls it for every reference that an added entity holds, or that an update changes, and whose
    /// row it finds in the database. A row that is not found is let through, as in <see cref="VerifyExisting(object, TenantWriteKind)"/>: it
    /// cannot belong to another tenant, and the store's own foreign key reports it. A row added in the same commit
    /// need not be checked: <see cref="ApplyToAdded"/> already gives it the current tenant.
    /// </para>
    /// <para>
    /// The message does not say whether the row exists, only that the current tenant cannot refer to it.
    /// </para>
    /// </remarks>
    public void VerifyReference(
        object entity,
        Type principalType,
        IReadOnlyList<string> foreignKeyProperties,
        object? principalTenantId)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(principalType);
        ArgumentNullException.ThrowIfNull(foreignKeyProperties);

        if (IsHostScope)
            return;

        var current = RequireCurrentTenant(principalType);

        if (!Equals(principalTenantId, current))
        {
            var entityType = entity.GetType();

            throw new TenantViolationException(
                $"Cannot save the {entityType.Name}: its {string.Join(", ", foreignKeyProperties)} refers to a " +
                $"{principalType.Name} that the current tenant cannot see. A row can only refer to rows of its own " +
                "tenant. Nothing was written.",
                entityType);
        }
    }

    private static string DescribeAction(TenantWriteKind kind) => kind == TenantWriteKind.Delete ? "delete" : "update";
}
