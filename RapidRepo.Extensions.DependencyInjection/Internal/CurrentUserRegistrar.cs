using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RapidRepo.Auditing;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

/// <summary>
/// Registers the current-user providers and the strict-mode options configured in <see cref="RapidRepoOptions"/>.
/// </summary>
internal static class CurrentUserRegistrar
{
    internal static void Register(IServiceCollection services, RapidRepoOptions options)
    {
        // Same lifetime as the units of work, so a per-request provider is never captured by a longer-lived one.
        foreach (var registration in options.CurrentUserProviderRegistrations)
            services.TryAdd(registration.CreateDescriptor(options.Lifetime));

        if (options.RequireCurrentUser)
            services.TryAddSingleton(new CurrentUserOptions { RequireCurrentUser = true });
    }
}
