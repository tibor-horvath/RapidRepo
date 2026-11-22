namespace RapidRepo.Extensions.DependencyInjection;

/// <summary>
/// A check a provider registers to run when the host starts, and from
/// <see cref="Microsoft.Extensions.DependencyInjection.RapidRepoServiceProviderExtensions.ValidateRapidRepoTenancy"/>,
/// for example the EF Core tenancy validation of a context's model.
/// </summary>
public interface IRapidRepoStartupValidator
{
    /// <summary>
    /// Runs the check.
    /// </summary>
    /// <param name="services">The services of a scope created for the validation.</param>
    /// <returns>The problems found, each naming what is wrong and how to fix it; empty when there are none.</returns>
    IReadOnlyList<string> Validate(IServiceProvider services);
}
