using RapidRepo.Auditing;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

/// <summary>
/// The <see cref="ICurrentUserProvider{TUserKey}"/> behind <see cref="RapidRepoOptions.UseCurrentUser{TUserKey}"/>.
/// </summary>
internal sealed class DelegateCurrentUserProvider<TUserKey>(
    IServiceProvider serviceProvider,
    Func<IServiceProvider, TUserKey?> getCurrentUserId) : ICurrentUserProvider<TUserKey>
    where TUserKey : struct
{
    public TUserKey? GetCurrentUserId() => getCurrentUserId(serviceProvider);
}
