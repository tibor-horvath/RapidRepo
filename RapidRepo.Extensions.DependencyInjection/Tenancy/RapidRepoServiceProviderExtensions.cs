using RapidRepo.Extensions.DependencyInjection;
using RapidRepo.Extensions.DependencyInjection.Internal;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// RapidRepo checks to run against a built <see cref="IServiceProvider"/>.
/// </summary>
public static class RapidRepoServiceProviderExtensions
{
    /// <summary>
    /// Runs the multi-tenancy validation that a hosted application runs at startup: for every registered provider,
    /// that each tenant entity has the tenant filter, nothing can replace it, and the tenant can be resolved. Call
    /// it in apps without a host, such as console tools, and in tests.
    /// </summary>
    /// <param name="services">The application's service provider.</param>
    /// <exception cref="RapidRepo.RapidRepoConfigurationException">
    /// The configuration is invalid; <see cref="RapidRepo.RapidRepoConfigurationException.Problems"/> lists every problem.
    /// </exception>
    public static void ValidateRapidRepoTenancy(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        StartupValidation.Run(services);
    }
}
