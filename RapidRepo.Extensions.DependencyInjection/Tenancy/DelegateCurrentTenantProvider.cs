using RapidRepo.Tenancy;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

/// <summary>
/// The <see cref="ICurrentTenantProvider{TTenantKey}"/> behind <see cref="RapidRepoOptions.UseCurrentTenant{TTenantKey}"/>.
/// </summary>
internal sealed class DelegateCurrentTenantProvider<TTenantKey>(
    IServiceProvider serviceProvider,
    Func<IServiceProvider, TTenantKey?> getCurrentTenantId) : ICurrentTenantProvider<TTenantKey>
    where TTenantKey : notnull
{
    public TTenantKey? GetCurrentTenantId() => getCurrentTenantId(serviceProvider);
}
