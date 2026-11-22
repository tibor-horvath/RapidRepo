namespace RapidRepo.Tenancy;

/// <summary>
/// The exception thrown when a read or a commit would cross a tenant boundary, or runs without a tenant.
/// </summary>
/// <remarks>
/// <para>
/// A commit that throws it writes nothing: the tenant rules are checked before any change is persisted.
/// </para>
/// <para>
/// Map it to HTTP 403 or 404 rather than 409. Unlike a <see cref="Concurrency.ConcurrencyException"/>, retrying does
/// not help: the caller is not allowed to touch the row.
/// </para>
/// </remarks>
public sealed class TenantViolationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantViolationException"/> class.
    /// </summary>
    public TenantViolationException()
        : this("The operation would cross a tenant boundary.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantViolationException"/> class with a message.
    /// </summary>
    /// <param name="message">The message.</param>
    public TenantViolationException(string message)
        : this(message, entityType: null, innerException: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantViolationException"/> class with a message and an inner
    /// exception.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The exception that caused this one, if any.</param>
    public TenantViolationException(string message, Exception? innerException)
        : this(message, entityType: null, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantViolationException"/> class for an entity type.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="entityType">The entity type the violation concerns, if any.</param>
    /// <param name="innerException">The exception that caused this one, if any.</param>
    public TenantViolationException(string message, Type? entityType, Exception? innerException = null)
        : base(message, innerException)
    {
        EntityType = entityType;
    }

    /// <summary>
    /// Gets the entity type the violation concerns, or <see langword="null"/> when it is not about one type.
    /// </summary>
    public Type? EntityType { get; }

    /// <summary>
    /// Creates the exception for reading or writing a tenant entity without a current tenant. Shared by the core and
    /// the providers, so the guidance is the same everywhere.
    /// </summary>
    /// <param name="entityType">The tenant entity type.</param>
    /// <param name="operation">What was attempted, for example "read" or "commit".</param>
    /// <returns>The exception.</returns>
    public static TenantViolationException MissingTenant(Type entityType, string operation)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        return new(
            $"Cannot {operation} {entityType.Name}: it is a tenant entity and there is no current tenant. Configure a " +
            "tenant provider that returns the tenant of the request, or run this code inside an explicit scope: " +
            "ITenantScope<TTenantKey>.Override(tenantId) to act as one tenant, or ITenantHostScope.BeginHostScope() " +
            "to act across tenants.",
            entityType);
    }
}
