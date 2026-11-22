using Microsoft.Extensions.DependencyInjection;
using RapidRepo.Extensions.DependencyInjection.Internal;
using RapidRepo.Tenancy;

namespace RapidRepo.Extensions.DependencyInjection;

public sealed partial class RapidRepoOptions
{
    private readonly List<ProviderRegistration> _currentTenantProviderRegistrations = [];

    /// <summary>
    /// Run the multi-tenancy validation when the host starts, so that <c>host.StartAsync()</c> fails on a
    /// misconfiguration (a tenant entity without the tenant filter, a context that cannot supply the tenant, ...).
    /// Applies only when a tenant provider is configured (see <see cref="UseCurrentTenantProvider{TProvider,TTenantKey}"/>);
    /// otherwise nothing is registered. Turning it off does not turn off the validation the provider runs on first use.
    /// Default: <c>true</c>.
    /// </summary>
    public bool ValidateTenancyOnStartup { get; set; } = true;

    internal IReadOnlyList<ProviderRegistration> CurrentTenantProviderRegistrations => _currentTenantProviderRegistrations;

    /// <summary>
    /// Register <typeparamref name="TProvider"/> as the <see cref="ICurrentTenantProvider{TTenantKey}"/> that supplies
    /// the tenant of the current request. It is registered with <see cref="Lifetime"/>, so it can depend on
    /// per-request services. This also turns on the startup validation (see <see cref="ValidateTenancyOnStartup"/>).
    /// </summary>
    /// <remarks>
    /// Units of work pick the provider up from the <see cref="IServiceProvider"/> passed to their constructor. For
    /// EF Core, the context reads the tenant through <c>ITenantDbContext&lt;TTenantKey&gt;</c>, usually by taking this
    /// provider as a constructor parameter. A registration the application already made for
    /// <see cref="ICurrentTenantProvider{TTenantKey}"/> takes precedence.
    /// </remarks>
    /// <typeparam name="TProvider">The provider implementation.</typeparam>
    /// <typeparam name="TTenantKey">The tenant key type, the same as the entities'.</typeparam>
    /// <returns>The same options, for chaining.</returns>
    /// <exception cref="ArgumentException"><typeparamref name="TProvider"/> is abstract.</exception>
    /// <exception cref="InvalidOperationException">
    /// A tenant provider has already been configured for <typeparamref name="TTenantKey"/>.
    /// </exception>
    public RapidRepoOptions UseCurrentTenantProvider<TProvider, TTenantKey>()
        where TProvider : class, ICurrentTenantProvider<TTenantKey>
        where TTenantKey : notnull
    {
        EnsureConcrete(typeof(TProvider), ProviderKind.CurrentTenant.Name, nameof(TProvider));

        return AddProvider(
            _currentTenantProviderRegistrations,
            ProviderKind.CurrentTenant,
            typeof(TTenantKey),
            lifetime => new ServiceDescriptor(typeof(ICurrentTenantProvider<TTenantKey>), typeof(TProvider), lifetime));
    }

    /// <summary>
    /// Register a delegate as the <see cref="ICurrentTenantProvider{TTenantKey}"/> that supplies the tenant of the
    /// current request, for example
    /// <c>UseCurrentTenant&lt;Guid&gt;(sp =&gt; sp.GetRequiredService&lt;IHttpContextAccessor&gt;().HttpContext?.User.GetTenantId() ?? Guid.Empty)</c>.
    /// This also turns on the startup validation (see <see cref="ValidateTenancyOnStartup"/>).
    /// </summary>
    /// <remarks>
    /// The delegate runs with the service provider of the scope the provider was resolved in, each time the tenant
    /// is needed. It returns <see langword="null"/> (or, for a value-type key, the default value) when there is none.
    /// </remarks>
    /// <typeparam name="TTenantKey">The tenant key type, the same as the entities'.</typeparam>
    /// <param name="getCurrentTenantId">Returns the current tenant.</param>
    /// <returns>The same options, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="getCurrentTenantId"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// A tenant provider has already been configured for <typeparamref name="TTenantKey"/>.
    /// </exception>
    public RapidRepoOptions UseCurrentTenant<TTenantKey>(Func<IServiceProvider, TTenantKey?> getCurrentTenantId)
        where TTenantKey : notnull
    {
        ArgumentNullException.ThrowIfNull(getCurrentTenantId);

        return AddProvider(
            _currentTenantProviderRegistrations,
            ProviderKind.CurrentTenant,
            typeof(TTenantKey),
            lifetime => new ServiceDescriptor(
                typeof(ICurrentTenantProvider<TTenantKey>),
                sp => new DelegateCurrentTenantProvider<TTenantKey>(sp, getCurrentTenantId),
                lifetime));
    }
}
