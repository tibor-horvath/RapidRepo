using Microsoft.Extensions.DependencyInjection;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

internal static class RepositoryDescriptorBuilder
{
    internal static List<ServiceDescriptor> Build(
        IReadOnlyList<(Type ConcreteType, IReadOnlyList<Type> RegistrationInterfaces)> candidates,
        ServiceLifetime lifetime,
        bool registerAsSelf,
        bool throwOnAmbiguous)
    {
        if (throwOnAmbiguous)
            CheckAmbiguity(candidates);

        var result = new List<ServiceDescriptor>();

        foreach (var (concrete, interfaces) in candidates)
        {
            foreach (var iface in interfaces)
                result.Add(new ServiceDescriptor(iface, concrete, lifetime));

            if (registerAsSelf)
                result.Add(new ServiceDescriptor(concrete, concrete, lifetime));
        }

        return result;
    }

    private static void CheckAmbiguity(
        IReadOnlyList<(Type ConcreteType, IReadOnlyList<Type> RegistrationInterfaces)> candidates)
    {
        var interfaceToConcretes = new Dictionary<Type, List<Type>>();

        foreach (var (concrete, interfaces) in candidates)
        {
            foreach (var iface in interfaces)
            {
                if (!interfaceToConcretes.TryGetValue(iface, out var list))
                {
                    list = [];
                    interfaceToConcretes[iface] = list;
                }
                list.Add(concrete);
            }
        }

        var ambiguous = interfaceToConcretes.Where(kv => kv.Value.Count > 1).ToList();
        if (ambiguous.Count == 0)
            return;

        var details = string.Join("; ", ambiguous.Select(kv =>
            $"{kv.Key.Name} is implemented by [{string.Join(", ", kv.Value.Select(t => t.Name))}]"));

        // A generic interface shared by several repositories is usually a persistence provider's own interface,
        // which scanning only treats as a root when that provider is configured.
        var providerHint = ambiguous.Any(kv => kv.Key.IsGenericType)
            ? " If a generic interface listed here belongs to a persistence provider, configure that provider " +
              "(for example UseEntityFrameworkCore()) so scanning treats it as a root interface."
            : "";

        throw new InvalidOperationException(
            $"Ambiguous repository registrations detected. {details}. " +
            $"Set {nameof(RapidRepoOptions.ThrowOnAmbiguousRegistration)} = false to suppress this error." +
            providerHint);
    }
}
