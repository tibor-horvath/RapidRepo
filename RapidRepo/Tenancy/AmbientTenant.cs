using System.ComponentModel;

namespace RapidRepo.Tenancy;

/// <summary>
/// The tenant scope that applies to the current asynchronous flow, as opened by
/// <see cref="ITenantScope{TTenantKey}.Override"/> and <see cref="Host.ITenantHostScope.BeginHostScope"/>.
/// </summary>
/// <remarks>
/// For providers: read it where the current tenant is resolved, before asking the tenant provider. Application code
/// opens scopes through <see cref="ITenantScope{TTenantKey}"/> and <see cref="Host.ITenantHostScope"/> instead.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class AmbientTenant
{
    private static readonly AsyncLocal<Frame?> CurrentFrame = new();

    /// <summary>
    /// Gets a value indicating whether the innermost scope is a host scope, in which tenant filters and write guards
    /// are lifted.
    /// </summary>
    public static bool IsHostScope => CurrentFrame.Value is { IsHost: true };

    /// <summary>
    /// Gets the tenant the innermost scope overrides for <paramref name="tenantKeyType"/>.
    /// </summary>
    /// <param name="tenantKeyType">The tenant key type.</param>
    /// <param name="tenantId">The overriding tenant, when the method returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the innermost scope is an override for a tenant of <paramref name="tenantKeyType"/>;
    /// <see langword="false"/> when there is no scope, the innermost one is a host scope, or it overrides another key
    /// type.
    /// </returns>
    public static bool TryGetOverride(Type tenantKeyType, out object? tenantId)
    {
        ArgumentNullException.ThrowIfNull(tenantKeyType);

        if (CurrentFrame.Value is { IsHost: false } frame && frame.TenantKeyType == tenantKeyType)
        {
            tenantId = frame.TenantId;
            return true;
        }

        tenantId = null;
        return false;
    }

    /// <summary>
    /// Resolves the current tenant for <paramref name="tenantKeyType"/>: the innermost override, otherwise the
    /// answer of <paramref name="getSourceTenant"/>. This is the one place that order is decided, for reads and
    /// commits alike.
    /// </summary>
    /// <param name="tenantKeyType">The tenant key type.</param>
    /// <param name="getSourceTenant">Asks the tenant source; only called when no override applies.</param>
    /// <param name="sourceName">The source, named in the error message.</param>
    /// <returns>The tenant, boxed, or <see langword="null"/> when there is none.</returns>
    /// <exception cref="TenantViolationException">The source returned an empty string.</exception>
    public static object? ResolveCurrentTenant(Type tenantKeyType, Func<object?> getSourceTenant, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(getSourceTenant);
        ArgumentNullException.ThrowIfNull(sourceName);

        return TryGetOverride(tenantKeyType, out var tenantId)
            ? tenantId
            : TenantKeys.Normalize(getSourceTenant(), sourceName);
    }

    /// <summary>
    /// Resolves the current tenant like <see cref="ResolveCurrentTenant"/>, but treats an empty string from the
    /// source as no tenant instead of throwing. For code that cannot throw, such as a query filter.
    /// </summary>
    /// <param name="tenantKeyType">The tenant key type.</param>
    /// <param name="getSourceTenant">Asks the tenant source; only called when no override applies.</param>
    /// <returns>The tenant, boxed, or <see langword="null"/> when there is none.</returns>
    public static object? ResolveCurrentTenantOrNull(Type tenantKeyType, Func<object?> getSourceTenant)
    {
        ArgumentNullException.ThrowIfNull(getSourceTenant);

        return TryGetOverride(tenantKeyType, out var tenantId)
            ? tenantId
            : TenantKeys.ToTenantOrNull(getSourceTenant());
    }

    internal static IDisposable Push(bool isHost, Type? tenantKeyType, object? tenantId)
    {
        var frame = new Frame(CurrentFrame.Value, isHost, tenantKeyType, tenantId);
        CurrentFrame.Value = frame;
        return frame;
    }

    private sealed class Frame(Frame? parent, bool isHost, Type? tenantKeyType, object? tenantId) : IDisposable
    {
        private bool _disposed;

        public bool IsHost { get; } = isHost;

        public Type? TenantKeyType { get; } = tenantKeyType;

        public object? TenantId { get; } = tenantId;

        public void Dispose()
        {
            if (_disposed)
                return;

            if (!ReferenceEquals(CurrentFrame.Value, this))
            {
                throw new InvalidOperationException(
                    "A tenant scope was disposed out of order, or in a different asynchronous flow than the one that " +
                    "opened it. Dispose tenant scopes in reverse order of opening, with 'using'.");
            }

            _disposed = true;
            CurrentFrame.Value = parent;
        }
    }
}
