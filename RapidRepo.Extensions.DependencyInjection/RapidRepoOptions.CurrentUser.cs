using Microsoft.Extensions.DependencyInjection;
using RapidRepo.Auditing;
using RapidRepo.Extensions.DependencyInjection.Internal;

namespace RapidRepo.Extensions.DependencyInjection;

public sealed partial class RapidRepoOptions
{
    private readonly List<ProviderRegistration> _currentUserProviderRegistrations = [];

    /// <summary>
    /// Make a commit that has no audit user throw when it is about to stamp a user field (<c>CreatedBy</c>,
    /// <c>ModifiedBy</c>, <c>DeletedBy</c>), instead of stamping the unit of work's default user key. A commit has
    /// no user when no <c>userId</c> is passed and the current-user provider (see
    /// <see cref="UseCurrentUserProvider{TProvider,TUserKey}"/>) returns <see langword="null"/> or is not configured.
    /// A commit that stamps no user field succeeds without a user.
    /// Registers <see cref="CurrentUserOptions"/>, which a unit of work reads from the
    /// <see cref="IServiceProvider"/> passed to its constructor. Default: <c>false</c>.
    /// </summary>
    public bool RequireCurrentUser { get; set; }

    internal IReadOnlyList<ProviderRegistration> CurrentUserProviderRegistrations => _currentUserProviderRegistrations;

    /// <summary>
    /// Register <typeparamref name="TProvider"/> as the <see cref="ICurrentUserProvider{TUserKey}"/> that units of
    /// work ask for the audit user when a commit names none. It is registered with <see cref="Lifetime"/>, the
    /// lifetime of the units of work, so it can depend on per-request services.
    /// </summary>
    /// <remarks>
    /// A unit of work picks the provider up from the <see cref="IServiceProvider"/> passed to its constructor, or
    /// takes it as a constructor parameter of its own. A registration the application already made for
    /// <see cref="ICurrentUserProvider{TUserKey}"/> takes precedence.
    /// </remarks>
    /// <typeparam name="TProvider">The provider implementation.</typeparam>
    /// <typeparam name="TUserKey">The type of the user key, the same as the unit of work's.</typeparam>
    /// <returns>The same options, for chaining.</returns>
    /// <exception cref="ArgumentException"><typeparamref name="TProvider"/> is abstract.</exception>
    /// <exception cref="InvalidOperationException">
    /// A current-user provider has already been configured for <typeparamref name="TUserKey"/>.
    /// </exception>
    public RapidRepoOptions UseCurrentUserProvider<TProvider, TUserKey>()
        where TProvider : class, ICurrentUserProvider<TUserKey>
        where TUserKey : struct
    {
        EnsureConcrete(typeof(TProvider), ProviderKind.CurrentUser.Name, nameof(TProvider));

        return AddProvider(
            _currentUserProviderRegistrations,
            ProviderKind.CurrentUser,
            typeof(TUserKey),
            lifetime => new ServiceDescriptor(typeof(ICurrentUserProvider<TUserKey>), typeof(TProvider), lifetime));
    }

    /// <summary>
    /// Register a delegate as the <see cref="ICurrentUserProvider{TUserKey}"/> that units of work ask for the audit
    /// user when a commit names none, for example
    /// <c>UseCurrentUser&lt;Guid&gt;(sp =&gt; sp.GetRequiredService&lt;IHttpContextAccessor&gt;().HttpContext?.User.GetUserId())</c>.
    /// </summary>
    /// <remarks>
    /// The delegate runs once per commit that names no user, with the service provider of the unit of work's
    /// scope. The provider is registered with <see cref="Lifetime"/>, the lifetime of the units of work.
    /// </remarks>
    /// <typeparam name="TUserKey">The type of the user key, the same as the unit of work's.</typeparam>
    /// <param name="getCurrentUserId">Returns the current user, or <see langword="null"/> when there is none.</param>
    /// <returns>The same options, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="getCurrentUserId"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// A current-user provider has already been configured for <typeparamref name="TUserKey"/>.
    /// </exception>
    public RapidRepoOptions UseCurrentUser<TUserKey>(Func<IServiceProvider, TUserKey?> getCurrentUserId)
        where TUserKey : struct
    {
        ArgumentNullException.ThrowIfNull(getCurrentUserId);

        return AddProvider(
            _currentUserProviderRegistrations,
            ProviderKind.CurrentUser,
            typeof(TUserKey),
            lifetime => new ServiceDescriptor(
                typeof(ICurrentUserProvider<TUserKey>),
                sp => new DelegateCurrentUserProvider<TUserKey>(sp, getCurrentUserId),
                lifetime));
    }
}
