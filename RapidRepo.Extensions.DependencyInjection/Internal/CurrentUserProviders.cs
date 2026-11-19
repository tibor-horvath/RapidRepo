using Microsoft.Extensions.DependencyInjection;
using RapidRepo.Auditing;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

/// <summary>
/// A current-user provider configured on <see cref="RapidRepoOptions"/>. The descriptor is created in
/// <c>AddRapidRepo</c>, so it gets the final <see cref="RapidRepoOptions.Lifetime"/> whatever order the options
/// were set in.
/// </summary>
internal sealed record CurrentUserProviderRegistration(
    Type UserKeyType,
    Func<ServiceLifetime, ServiceDescriptor> CreateDescriptor);

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
