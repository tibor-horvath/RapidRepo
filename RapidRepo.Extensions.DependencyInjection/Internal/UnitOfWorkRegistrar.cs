using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

internal static class UnitOfWorkRegistrar
{
    internal static void Register(
        IServiceCollection services,
        Type interfaceType,
        Type implementationType,
        ServiceLifetime lifetime)
    {
        services.TryAdd(new ServiceDescriptor(interfaceType, implementationType, lifetime));

        // Forward each closed IUnitOfWork<TUserKey> that TImpl implements,
        // unless it is already the user-supplied TInterface.
        foreach (var closedUoW in GetClosedUnitOfWorkInterfaces(implementationType))
        {
            if (closedUoW == interfaceType)
                continue;

            services.TryAdd(new ServiceDescriptor(closedUoW, implementationType, lifetime));
        }
    }

    private static IEnumerable<Type> GetClosedUnitOfWorkInterfaces(Type implementationType)
    {
        return implementationType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUnitOfWork<>));
    }
}
