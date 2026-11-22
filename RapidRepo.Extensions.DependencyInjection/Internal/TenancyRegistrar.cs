using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RapidRepo.Tenancy;
using RapidRepo.Tenancy.Host;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

/// <summary>
/// Registers the tenant providers, the tenant scopes and the startup validation configured in
/// <see cref="RapidRepoOptions"/>.
/// </summary>
internal static class TenancyRegistrar
{
    /// <summary>
    /// Registers only when this call configured a tenant provider: an application without tenancy gets no new
    /// services and never resolves its context at startup.
    /// </summary>
    internal static void Register(IServiceCollection services, RapidRepoOptions options)
    {
        if (options.CurrentTenantProviderRegistrations.Count == 0)
            return;

        services.TryAddSingleton(typeof(ITenantScope<>), typeof(LoggingTenantScope<>));
        services.TryAddSingleton<ITenantHostScope, LoggingTenantHostScope>();

        // Same lifetime as the units of work, so a per-request provider is never captured by a longer-lived one.
        foreach (var registration in options.CurrentTenantProviderRegistrations)
            services.TryAdd(registration.CreateDescriptor(options.Lifetime));

        if (options.ValidateTenancyOnStartup)
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TenancyStartupValidationService>());
    }
}
