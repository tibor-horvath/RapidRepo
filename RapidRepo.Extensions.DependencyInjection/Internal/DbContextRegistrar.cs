using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RapidRepo.Entities;
using RapidRepo.Repositories;
using RapidRepo.Repositories.Interfaces;
using System.Reflection;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

internal static class DbContextRegistrar
{
    private static readonly Type[] RootInterfaces =
    [
        typeof(IRepository<,>),
        typeof(IReadOnlyRepository<,>),
        typeof(IWriteRepository<,>)
    ];

    /// <summary>
    /// Registers the <see cref="DbContext"/> → <paramref name="contextType"/> forwarder, but only when a type
    /// registered by this call takes a base <see cref="DbContext"/> constructor parameter. Skipping it otherwise
    /// keeps two contexts from competing for the single <see cref="DbContext"/> service slot in applications
    /// where every repository already names its own derived context.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A forwarder is required but a different context already claims the <see cref="DbContext"/> slot.
    /// </exception>
    internal static void RegisterForwarder(
        IServiceCollection services,
        Type contextType,
        IEnumerable<Type> dependentTypes)
    {
        if (!dependentTypes.Any(TakesBaseDbContext))
            return;

        // The forwarder has to be a factory so it hands out the very instance AddDbContext registered —
        // a type-based DbContext -> TContext descriptor would build a second, separate context. A factory
        // descriptor exposes no ImplementationType, hence the marker recording what we pointed it at.
        if (FindMarker(services) is { } marker)
        {
            if (marker.ContextType == contextType)
                return;

            throw new InvalidOperationException(
                $"Cannot register the DbContext forwarder for '{contextType.Name}': the base DbContext service " +
                $"is already forwarded to '{marker.ContextType.Name}'. Only one context can occupy that slot, so " +
                "repositories and units of work that take a 'DbContext' constructor parameter are ambiguous " +
                "across contexts. Change them to take their own derived context type " +
                $"(for example '{contextType.Name}') instead.");
        }

        // An application that wired its own forwarder before calling AddRapidRepo keeps it.
        if (services.Any(d => d.ServiceType == typeof(DbContext)))
            return;

        // Scoped regardless of options.Lifetime: this only forwards to the context's own registration,
        // and a longer-lived forwarder would capture a scoped context.
        services.AddScoped(typeof(DbContext), sp => sp.GetRequiredService(contextType));
        services.AddSingleton(new ForwarderMarker(contextType));
    }

    private static ForwarderMarker? FindMarker(IServiceCollection services)
        => services.FirstOrDefault(d => d.ServiceType == typeof(ForwarderMarker))?
            .ImplementationInstance as ForwarderMarker;

    /// <summary>
    /// Records which context <see cref="RegisterForwarder"/> pointed the base <see cref="DbContext"/> at, so a
    /// later <c>AddRapidRepo</c> call can tell an existing RapidRepo forwarder from an application-supplied one.
    /// </summary>
    private sealed class ForwarderMarker(Type contextType)
    {
        internal Type ContextType { get; } = contextType;
    }

    /// <summary>
    /// Registers a closed repository per <see cref="DbSet{TEntity}"/> on <paramref name="contextType"/>, bound to
    /// that context via <see cref="Repository{TEntity, TId, TContext}"/>. Closed registrations win over any
    /// open-generic fallback, which is what keeps each context serving its own entities.
    /// </summary>
    internal static void RegisterClosedGenericRepositories(
        IServiceCollection services,
        Type contextType,
        ServiceLifetime lifetime)
    {
        foreach (var (entityType, keyType) in DiscoverEntities(contextType))
        {
            var implementationType = typeof(Repository<,,>).MakeGenericType(entityType, keyType, contextType);

            foreach (var rootInterface in RootInterfaces)
            {
                var serviceType = rootInterface.MakeGenericType(entityType, keyType);
                services.TryAdd(ServiceDescriptor.Describe(serviceType, implementationType, lifetime));
            }
        }
    }

    /// <summary>
    /// Entity types exposed as <c>DbSet&lt;TEntity&gt;</c> properties on <paramref name="contextType"/> that derive
    /// from <see cref="BaseEntity{TId}"/>, paired with their key type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read by reflection rather than from the EF model, which would require building the context. Entities EF
    /// discovers only by navigation — with no <see cref="DbSet{TEntity}"/> of their own — are therefore not seen.
    /// </para>
    /// <para>
    /// Mirrors how EF itself finds sets: over runtime properties, so non-public <see cref="DbSet{TEntity}"/>
    /// declarations count. Looking at public properties alone would skip entities EF does map.
    /// </para>
    /// </remarks>
    internal static IEnumerable<(Type EntityType, Type KeyType)> DiscoverEntities(Type contextType)
    {
        var seen = new HashSet<Type>();

        foreach (var property in contextType.GetRuntimeProperties())
        {
            if (property.GetMethod is null or { IsStatic: true } || property.GetIndexParameters().Length > 0)
                continue;

            var propertyType = property.PropertyType;

            if (!propertyType.IsGenericType || propertyType.GetGenericTypeDefinition() != typeof(DbSet<>))
                continue;

            var entityType = propertyType.GenericTypeArguments[0];
            var keyType = GetEntityKeyType(entityType);

            // A property hidden by 'new' in a derived context surfaces twice.
            if (keyType is not null && seen.Add(entityType))
                yield return (entityType, keyType);
        }
    }

    private static bool TakesBaseDbContext(Type type)
        => type.GetConstructors()
            .Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(DbContext)));

    private static Type? GetEntityKeyType(Type entityType)
    {
        for (var type = entityType; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BaseEntity<>))
                return type.GenericTypeArguments[0];
        }

        return null;
    }
}
