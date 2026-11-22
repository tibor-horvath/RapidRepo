namespace RapidRepo.Tenancy;

/// <summary>
/// The default tenant source of a unit of work: the <see cref="ICurrentTenantProvider{TTenantKey}"/> registered in
/// the service provider it was given.
/// </summary>
internal static class ServiceProviderTenantSource
{
    /// <summary>
    /// Asks the <see cref="ICurrentTenantProvider{TTenantKey}"/> for <paramref name="tenantKeyType"/> registered in
    /// <paramref name="serviceProvider"/>, and returns its answer as it is.
    /// </summary>
    /// <returns>The tenant, boxed, or <see langword="null"/> when no provider is registered.</returns>
    public static object? GetCurrentTenantId(IServiceProvider? serviceProvider, Type tenantKeyType)
    {
        if (serviceProvider?.GetService(typeof(ICurrentTenantProvider<>).MakeGenericType(tenantKeyType)) is not { } provider)
            return null;

        return CurrentTenantProviders.GetCurrentTenantId(provider, tenantKeyType);
    }

    /// <summary>Names the source, for the error raised when it returns an empty string.</summary>
    public static string SourceName(Type tenantKeyType) =>
        $"The {nameof(ICurrentTenantProvider<int>)}<{tenantKeyType.Name}>";
}
