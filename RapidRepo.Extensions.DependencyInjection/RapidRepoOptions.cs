using Microsoft.Extensions.DependencyInjection;
using RapidRepo.Extensions.DependencyInjection.Internal;

namespace RapidRepo.Extensions.DependencyInjection;

/// <summary>
/// Configuration options for <see cref="RapidRepoServiceCollectionExtensions.AddRapidRepo"/>.
/// </summary>
/// <remarks>
/// Split into partial files by concern: scanning, units of work, current user and tenancy.
/// </remarks>
public sealed partial class RapidRepoOptions
{
    /// <summary>Lifetime applied to every discovered repository. Default: <see cref="ServiceLifetime.Scoped"/>.</summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>Also register each concrete repository against its own type.</summary>
    public bool RegisterAsSelf { get; set; }

    /// <summary>Throw when two concrete types implement the same user-defined interface. Default: <c>true</c>.</summary>
    public bool ThrowOnAmbiguousRegistration { get; set; } = true;

    /// <summary>Throw instead of warn when <see cref="Lifetime"/> is <see cref="ServiceLifetime.Singleton"/>. Default: <c>false</c>.</summary>
    public bool ThrowOnSingletonMisuse { get; set; }

    /// <summary>
    /// Have the provider register its generic repository implementation for the root repository interfaces
    /// (<see cref="RapidRepoRegistrationContext.RootInterfaces"/>), so they can be injected without writing a
    /// repository class.
    /// Specific registrations from assembly scanning always take precedence.
    /// Requires a provider (see <see cref="UseProvider"/>). Default: <c>false</c>.
    /// </summary>
    public bool RegisterGenericRepositories { get; set; }

    internal IRapidRepoProvider? Provider { get; private set; }

    /// <summary>
    /// Use <paramref name="provider"/> for the provider-specific registrations of this <c>AddRapidRepo</c> call.
    /// Provider packages wrap this in their own extension, for example <c>UseEntityFrameworkCore&lt;TContext&gt;()</c>.
    /// </summary>
    /// <param name="provider">The persistence provider.</param>
    /// <returns>The same options, for chaining.</returns>
    /// <exception cref="InvalidOperationException">A provider has already been configured for this call.</exception>
    public RapidRepoOptions UseProvider(IRapidRepoProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (Provider is not null)
            throw new InvalidOperationException(
                $"A provider has already been configured for this AddRapidRepo call ('{Provider.Name}'). " +
                "Configure one provider per AddRapidRepo call, and use a separate AddRapidRepo call per provider " +
                "or data source.");

        Provider = provider;
        return this;
    }

    private RapidRepoOptions AddProvider(
        List<ProviderRegistration> registrations,
        ProviderKind kind,
        Type keyType,
        Func<ServiceLifetime, ServiceDescriptor> createDescriptor)
    {
        if (registrations.Any(r => r.KeyType == keyType))
            throw new InvalidOperationException(
                $"A {kind.Name} has already been configured for {kind.KeyName} type '{keyType.Name}'. " +
                $"Call {kind.Methods} once per {kind.KeyName} type.");

        registrations.Add(new ProviderRegistration(keyType, createDescriptor));
        return this;
    }

    private static void EnsureConcrete(Type implementationType, string role, string paramName)
    {
        if (implementationType.IsAbstract)
            throw new ArgumentException(
                $"'{implementationType.Name}' is abstract and cannot be used as a {role}. Provide a concrete class.",
                paramName);
    }
}
