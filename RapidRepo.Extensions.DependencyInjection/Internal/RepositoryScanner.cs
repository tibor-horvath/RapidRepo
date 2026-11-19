using RapidRepo.Repositories.Interfaces;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

internal static class RepositoryScanner
{
    /// <summary>
    /// The core's open generic repository interfaces. A provider adds its own through
    /// <see cref="IRapidRepoProvider.AdditionalRootInterfaces"/>.
    /// </summary>
    internal static readonly IReadOnlyList<Type> CoreRootInterfaces =
    [
        typeof(IReadOnlyRepository<,>),
        typeof(IWriteRepository<,>),
        typeof(IRepository<,>)
    ];

    /// <summary>
    /// Finds the concrete repositories in <paramref name="assemblies"/> and the user-defined interfaces each should
    /// be registered against.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <param name="rootInterfaces">
    /// The open generic repository interfaces, the core's and the provider's: never registered against, only the
    /// interfaces derived from them are.
    /// </param>
    internal static IEnumerable<(Type ConcreteType, IReadOnlyList<Type> RegistrationInterfaces)> GetCandidates(
        IEnumerable<Assembly> assemblies,
        IEnumerable<Type> rootInterfaces)
    {
        var roots = new HashSet<Type>(rootInterfaces);

        bool IsRootInterface(Type iface)
            => iface.IsGenericType && roots.Contains(iface.GetGenericTypeDefinition());

        bool DerivesFromAnyRootInterface(Type iface)
            => IsRootInterface(iface) || iface.GetInterfaces().Any(IsRootInterface);

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

                if (!allInterfaces.Any(IsRootInterface))
                    continue;

                var registrationInterfaces = allInterfaces
                    .Where(i => !IsRootInterface(i))
                    .Where(DerivesFromAnyRootInterface)
                    .ToArray();

                yield return (type, registrationInterfaces);
            }
        }
    }
}
