using RapidRepo.UnitOfWork;

namespace RapidRepo.Extensions.DependencyInjection;

public sealed partial class RapidRepoOptions
{
    private readonly List<(Type InterfaceType, Type ImplementationType)> _unitOfWorkRegistrations = [];

    internal IReadOnlyList<(Type InterfaceType, Type ImplementationType)> UnitOfWorkRegistrations => _unitOfWorkRegistrations;

    /// <summary>
    /// Register a unit of work. Registers <typeparamref name="TInterface"/> → <typeparamref name="TImpl"/>
    /// and also forwards any closed <c>IUnitOfWork&lt;TKey&gt;</c> that <typeparamref name="TImpl"/> implements.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="TInterface"/> is not an interface type, or <typeparamref name="TImpl"/> is abstract.
    /// </exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="TInterface"/> has already been registered.</exception>
    public RapidRepoOptions UseUnitOfWork<TInterface, TImpl>()
        where TInterface : class
        where TImpl : class, TInterface
    {
        if (!typeof(TInterface).IsInterface)
            throw new ArgumentException(
                $"'{typeof(TInterface).Name}' is not an interface type. TInterface must be an interface.",
                nameof(TInterface));

        EnsureConcrete(typeof(TImpl), "unit-of-work implementation", nameof(TImpl));
        return AddUnitOfWork(typeof(TInterface), typeof(TImpl));
    }

    /// <summary>
    /// Register a unit of work by automatically detecting the single user-defined
    /// <see cref="IUnitOfWork{TUserKey}"/>-derived interface implemented by <typeparamref name="TImpl"/>.
    /// Use the two-type overload <see cref="UseUnitOfWork{TInterface,TImpl}"/> when
    /// <typeparamref name="TImpl"/> has zero or more than one such interface.
    /// </summary>
    /// <exception cref="ArgumentException"><typeparamref name="TImpl"/> is abstract.</exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TImpl"/> implements no user-defined <see cref="IUnitOfWork{TUserKey}"/> interface,
    /// implements more than one, or the detected interface has already been registered.
    /// </exception>
    public RapidRepoOptions UseUnitOfWork<TImpl>()
        where TImpl : class
    {
        var implType = typeof(TImpl);

        EnsureConcrete(implType, "unit-of-work implementation", nameof(TImpl));

        // Collect user-defined interfaces that extend IUnitOfWork<TKey> but are not the root itself.
        var candidateInterfaces = implType.GetInterfaces()
            .Where(i => i.IsInterface
                        && !(i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUnitOfWork<>))
                        && i.GetInterfaces().Any(p => p.IsGenericType
                                                      && p.GetGenericTypeDefinition() == typeof(IUnitOfWork<>)))
            .ToList();

        if (candidateInterfaces.Count == 0)
            throw new InvalidOperationException(
                $"'{implType.Name}' does not implement any user-defined IUnitOfWork<> interface. " +
                $"Use UseUnitOfWork<TInterface, TImpl>() to register it explicitly, " +
                $"or directly against IUnitOfWork<TKey>.");

        if (candidateInterfaces.Count > 1)
            throw new InvalidOperationException(
                $"'{implType.Name}' implements multiple user-defined IUnitOfWork<> interfaces: " +
                $"[{string.Join(", ", candidateInterfaces.Select(i => i.Name))}]. " +
                $"Use UseUnitOfWork<TInterface, TImpl>() to specify the interface explicitly.");

        return AddUnitOfWork(candidateInterfaces[0], implType);
    }

    private RapidRepoOptions AddUnitOfWork(Type interfaceType, Type implementationType)
    {
        if (_unitOfWorkRegistrations.Any(r => r.InterfaceType == interfaceType))
            throw new InvalidOperationException(
                $"A unit of work has already been registered for interface '{interfaceType.Name}'. " +
                "Call UseUnitOfWork once per interface type.");

        _unitOfWorkRegistrations.Add((interfaceType, implementationType));
        return this;
    }
}
