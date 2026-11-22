using Microsoft.Extensions.DependencyInjection;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

/// <summary>
/// An ambient provider (current user, current tenant) configured on <see cref="RapidRepoOptions"/> for one key type.
/// The descriptor is created in <c>AddRapidRepo</c>, so it gets the final <see cref="RapidRepoOptions.Lifetime"/>
/// whatever order the options were set in.
/// </summary>
internal sealed record ProviderRegistration(Type KeyType, Func<ServiceLifetime, ServiceDescriptor> CreateDescriptor);
