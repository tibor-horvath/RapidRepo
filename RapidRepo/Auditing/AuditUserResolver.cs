namespace RapidRepo.Auditing;

/// <summary>
/// Resolves a unit of work's audit user: the current-user provider and the strict-mode setting, both looked up
/// lazily, and the error raised in strict mode when there is no user.
/// </summary>
/// <typeparam name="TUserKey">The type of the user key recorded in audit fields.</typeparam>
internal sealed class AuditUserResolver<TUserKey>
    where TUserKey : struct
{
    private readonly Lazy<ICurrentUserProvider<TUserKey>?> _currentUserProvider;
    private readonly Lazy<bool> _requireCurrentUser;

    /// <param name="serviceProvider">Where the provider and <see cref="CurrentUserOptions"/> are looked up, if anywhere.</param>
    /// <param name="currentUserProvider">The provider passed to the unit of work, which takes precedence.</param>
    public AuditUserResolver(IServiceProvider? serviceProvider, ICurrentUserProvider<TUserKey>? currentUserProvider)
    {
        // Resolved on first use, not here: the unit of work may be created before the request has a user.
        _currentUserProvider = new Lazy<ICurrentUserProvider<TUserKey>?>(() =>
            currentUserProvider
            ?? (ICurrentUserProvider<TUserKey>?)serviceProvider?.GetService(typeof(ICurrentUserProvider<TUserKey>)));

        _requireCurrentUser = new Lazy<bool>(() =>
            (serviceProvider?.GetService(typeof(CurrentUserOptions)) as CurrentUserOptions)?.RequireCurrentUser ?? false);
    }

    /// <summary>Gets the provider passed to the constructor, otherwise the one registered, or <see langword="null"/>.</summary>
    public ICurrentUserProvider<TUserKey>? CurrentUserProvider => _currentUserProvider.Value;

    /// <summary>Gets the strict-mode setting registered as <see cref="CurrentUserOptions"/>, or <see langword="false"/>.</summary>
    public bool RequireCurrentUser => _requireCurrentUser.Value;

    /// <summary>
    /// Resolves the commit's audit user: the explicit <paramref name="userId"/>, else the current-user provider,
    /// else <see langword="null"/>, which the audit stamper stamps as the default user key (or rejects in strict mode).
    /// </summary>
    public TUserKey? Resolve(TUserKey? userId) => userId ?? CurrentUserProvider?.GetCurrentUserId();

    /// <summary>The error a strict-mode commit raises when it has no user to stamp.</summary>
    public InvalidOperationException MissingUserException() => new(
        $"The commit has no audit user to stamp: no userId was passed to Commit/CommitAsync, and " +
        (CurrentUserProvider is null
            ? $"no ICurrentUserProvider<{typeof(TUserKey).Name}> is configured. "
            : $"the ICurrentUserProvider<{typeof(TUserKey).Name}> returned null. ") +
        "RequireCurrentUser is set, so the default user key is not used. Pass a userId, configure a " +
        "current-user provider, or turn RequireCurrentUser off for commits that run without a user.");
}
