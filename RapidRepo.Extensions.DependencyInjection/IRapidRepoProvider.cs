using Microsoft.Extensions.DependencyInjection;

namespace RapidRepo.Extensions.DependencyInjection;

/// <summary>
/// A persistence provider's part of <see cref="RapidRepoServiceCollectionExtensions.AddRapidRepo"/>: the registrations
/// that depend on how data is accessed, such as the generic repository implementation and its data source.
/// </summary>
/// <remarks>
/// Assembly scanning, ambiguity checks, filters and unit of work registration are provider-neutral and handled by
/// <c>AddRapidRepo</c> itself. A provider package exposes an extension on <see cref="RapidRepoOptions"/>, such as
/// <c>UseEntityFrameworkCore&lt;TContext&gt;()</c>, which calls <see cref="RapidRepoOptions.UseProvider"/>.
/// </remarks>
public interface IRapidRepoProvider
{
    /// <summary>
    /// Gets the provider's display name, used in diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the provider's own open generic repository interfaces, for example <c>IEfRepository&lt;,&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Scanning treats these like <c>IRepository&lt;,&gt;</c>: a repository is registered against the
    /// user-defined interfaces derived from them, never against these interfaces themselves.
    /// </remarks>
    IEnumerable<Type> AdditionalRootInterfaces { get; }

    /// <summary>
    /// Adds the provider's registrations. Called once per <c>AddRapidRepo</c> call, after the scanned repositories
    /// and the units of work have been registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="context">What this <c>AddRapidRepo</c> call registered and how it was configured.</param>
    void Register(IServiceCollection services, RapidRepoRegistrationContext context);
}
