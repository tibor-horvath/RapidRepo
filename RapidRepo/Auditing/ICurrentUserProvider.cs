namespace RapidRepo.Auditing;

/// <summary>
/// Supplies the user making the current changes, so a unit of work can stamp <c>CreatedBy</c>, <c>ModifiedBy</c>
/// and <c>DeletedBy</c> without every caller passing a user to <c>Commit</c>.
/// </summary>
/// <remarks>
/// <para>
/// At commit the audit user is resolved in this order: the <c>userId</c> argument of <c>Commit</c> /
/// <c>CommitAsync</c>, then this provider, then the unit of work's default user key. The provider is asked at most
/// once per commit, so every entity in one commit gets the same user.
/// </para>
/// <para>
/// The method is synchronous on purpose: it runs inside the commit, and the usual sources (the current
/// <c>HttpContext.User</c>, an ambient scope) are already in memory.
/// </para>
/// </remarks>
/// <typeparam name="TUserKey">The type of the user key recorded in audit fields.</typeparam>
public interface ICurrentUserProvider<TUserKey>
    where TUserKey : struct
{
    /// <summary>
    /// Gets the user making the current changes.
    /// </summary>
    /// <returns>
    /// The user, or <see langword="null"/> when there is none (a background job, an anonymous request). The unit
    /// of work then falls back to its default user key, or throws when <see cref="CurrentUserOptions.RequireCurrentUser"/>
    /// is set.
    /// </returns>
    TUserKey? GetCurrentUserId();
}
