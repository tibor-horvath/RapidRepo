using RapidRepo.Repositories.Interfaces;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

internal static class RepositoryScanner
{
    private static readonly HashSet<Type> RootInterfaces =
    [
        typeof(IReadOnlyRepository<,>),
        typeof(IWriteRepository<,>),
        typeof(IRepository<,>)
    ];

    internal static IEnumerable<(Type ConcreteType, IReadOnlyList<Type> RegistrationInterfaces)> GetCandidates(
        IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            IEnumerable<Type> types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).Cast<Type>();
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                    continue;

                if (type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                    continue;

                // Single GetInterfaces() call per type — result used for both qualification
                // and building the registration interface list.
                var allInterfaces = type.GetInterfaces();

                if (!allInterfaces.Any(IsClosedRootInterface))
                    continue;

                var registrationInterfaces = allInterfaces
                    .Where(i => !IsRootInterface(i))
                    .Where(DerivesFromAnyRootInterface)
                    .ToArray();

                yield return (type, registrationInterfaces);
            }
        }
    }

    private static bool IsClosedRootInterface(Type iface)
        => iface.IsGenericType && RootInterfaces.Contains(iface.GetGenericTypeDefinition());

    private static bool IsRootInterface(Type iface)
        => iface.IsGenericType && RootInterfaces.Contains(iface.GetGenericTypeDefinition());

    private static bool DerivesFromAnyRootInterface(Type iface)
    {
        if (IsClosedRootInterface(iface))
            return true;

        return iface.GetInterfaces().Any(IsClosedRootInterface);
    }
}
