namespace RapidRepo.Auditing;

/// <summary>
/// How a unit of work treats a commit that has no audit user.
/// </summary>
/// <remarks>
/// A unit of work reads these options from the <see cref="IServiceProvider"/> passed to its constructor.
/// <c>AddRapidRepo</c> registers them when <c>RequireCurrentUser</c> is set. Without a service provider, override
/// <c>UnitOfWorkBase&lt;TUserKey&gt;.RequireCurrentUser</c> instead.
/// </remarks>
public sealed class CurrentUserOptions
{
    /// <summary>
    /// Gets a value indicating whether a commit must have a user for the user audit fields it stamps. When
    /// <see langword="true"/>, a commit that is about to stamp <c>CreatedBy</c>, <c>ModifiedBy</c> or
    /// <c>DeletedBy</c>, without a user from the <c>userId</c> argument or the
    /// <see cref="ICurrentUserProvider{TUserKey}"/>, throws an <see cref="InvalidOperationException"/> instead of
    /// stamping the default user key. A commit that stamps no user field, for example one that changes only
    /// entities without user audit fields, succeeds without a user. Default: <see langword="false"/>.
    /// </summary>
    public bool RequireCurrentUser { get; init; }
}
