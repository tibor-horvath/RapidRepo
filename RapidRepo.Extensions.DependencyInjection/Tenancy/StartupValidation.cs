using Microsoft.Extensions.DependencyInjection;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

internal static class StartupValidation
{
    /// <summary>
    /// Runs every registered validator in a new scope and throws one exception listing every problem.
    /// </summary>
    public static void Run(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var problems = scope.ServiceProvider
            .GetServices<IRapidRepoStartupValidator>()
            .SelectMany(v => v.Validate(scope.ServiceProvider))
            .ToList();

        if (problems.Count > 0)
            throw new RapidRepoConfigurationException("RapidRepo's multi-tenancy configuration is invalid", problems);
    }
}
