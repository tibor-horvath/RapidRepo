namespace RapidRepo.Extensions.DependencyInjection.Internal;

/// <summary>
/// The wording of the configuration errors for one kind of ambient provider.
/// </summary>
/// <param name="Name">What the provider is, for example "tenant provider".</param>
/// <param name="KeyName">What its key is, for example "tenant key".</param>
/// <param name="Methods">The options methods that configure it.</param>
internal sealed record ProviderKind(string Name, string KeyName, string Methods)
{
    public static readonly ProviderKind CurrentUser = new(
        "current-user provider",
        "user key",
        $"{nameof(RapidRepoOptions.UseCurrentUserProvider)} or {nameof(RapidRepoOptions.UseCurrentUser)}");

    public static readonly ProviderKind CurrentTenant = new(
        "tenant provider",
        "tenant key",
        $"{nameof(RapidRepoOptions.UseCurrentTenantProvider)} or {nameof(RapidRepoOptions.UseCurrentTenant)}");
}
