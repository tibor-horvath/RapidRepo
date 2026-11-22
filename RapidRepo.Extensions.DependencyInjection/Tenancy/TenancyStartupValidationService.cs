using Microsoft.Extensions.Hosting;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

/// <summary>
/// Runs every <see cref="IRapidRepoStartupValidator"/> when the host starts, so a misconfigured application fails
/// <c>host.StartAsync()</c> instead of serving requests. Registered only when tenancy is configured.
/// </summary>
internal sealed class TenancyStartupValidationService(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        StartupValidation.Run(services);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
