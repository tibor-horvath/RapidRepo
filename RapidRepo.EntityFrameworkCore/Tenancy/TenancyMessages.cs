using RapidRepo.Extensions;

namespace RapidRepo.EntityFrameworkCore.Internal;

/// <summary>
/// The messages for a tenancy misconfiguration, shared by <c>AddTenantQueryFilters</c> (which fails while the model
/// is built) and <see cref="TenancyValidator"/> (which checks a built model), so the same problem reads the same way
/// wherever it is found.
/// </summary>
internal static class TenancyMessages
{
    private const string AddTenantQueryFilters = nameof(ModelBuilderExtensions.AddTenantQueryFilters);

    public static string KeyTypeMismatch(Type entityType, Type keyType, Type contextKeyType) =>
        $"'{entityType.Name}' is a tenant entity with tenant key type '{keyType.Name}', but the context's tenant key " +
        $"type is '{contextKeyType.Name}'. Every tenant entity of a context must use the context's tenant key type; " +
        "map entities with another key type in a separate context.";

    public static string MixedKeyTypes(Type contextType, IEnumerable<(Type EntityType, Type KeyType)> tenantTypes) =>
        $"The tenant entities of {contextType.Name} use different tenant key types " +
        $"({string.Join(", ", tenantTypes.GroupBy(t => t.KeyType).Select(g => $"{g.Key.Name}: {string.Join(", ", g.Select(t => t.EntityType.Name))}"))}). " +
        "Every tenant entity of a context must use the same key type; map the others in a separate context.";

    public static string RootNotTenantEntity(Type entityType, Type rootType) =>
        $"'{entityType.Name}' is a tenant entity, but the root of its inheritance hierarchy, '{rootType.Name}', is " +
        $"not. EF Core only allows query filters on the root entity type, so {AddTenantQueryFilters} cannot isolate " +
        $"'{entityType.Name}' rows. Implement ITenantEntity<TTenantKey> on '{rootType.Name}', or map " +
        $"'{entityType.Name}' outside that hierarchy.";

    public static string AnonymousFilter(Type entityType) =>
        $"'{entityType.Name}' has an anonymous query filter where the named tenant filter " +
        $"('{ModelBuilderExtensions.TenantFilterKey}') should be. {AddTenantQueryFilters} does not replace " +
        "it, which would silently drop any tenant condition it holds, and EF Core does not allow anonymous and named " +
        "filters together. Remove the tenant condition from the existing filter, register what is left of it as a " +
        "named filter (HasQueryFilter(\"MyFilter\", ...)), and call AddTenantQueryFilters(this). If the anonymous " +
        $"filter came from {nameof(ModelBuilderExtensions.AddSoftDeleteQueryFilters)} because of another " +
        "anonymous filter, fix that one. See 'Migrating a hand-written tenant filter' in the multi-tenancy docs.";

    public static string MissingTenantFilter(Type entityType) =>
        $"{entityType.Name} is a tenant entity without the tenant query filter, so reads would return every " +
        "tenant's rows. Call modelBuilder.AddTenantQueryFilters(this) in OnModelCreating, after the entity types are " +
        "configured.";

    public static string ReplacedTenantFilter(Type entityType) =>
        $"{entityType.Name} has a query filter named '{ModelBuilderExtensions.TenantFilterKey}' that is not the " +
        "tenant filter AddTenantQueryFilters registers, so reads may not be restricted to the current tenant. Don't " +
        "register a filter under that name; call modelBuilder.AddTenantQueryFilters(this) after any other " +
        "HasQueryFilter call for the entity.";

    public static string MissingTenantDbContext(Type contextType, Type keyType) =>
        $"{contextType.Name} maps tenant entities with tenant key type '{keyType.Name}' but does not implement " +
        $"ITenantDbContext<{keyType.Name}>, so neither the tenant filter nor the unit of work can find the current " +
        $"tenant. Implement ITenantDbContext<{keyType.Name}> on {contextType.Name}, usually by delegating " +
        "CurrentTenantId to an ICurrentTenantProvider.";

    public static string MissingTenantProvider(Type keyType) =>
        $"no ICurrentTenantProvider<{keyType.Name}> is registered. Configure one with " +
        $"UseCurrentTenantProvider<TProvider, {keyType.Name}>() or UseCurrentTenant<{keyType.Name}>(...) in AddRapidRepo.";

    public static string UnscopedForeignKey(Type dependentType, Type principalType, IEnumerable<string> properties, bool required)
    {
        var columns = string.Join(", ", properties);

        return $"The foreign key from {dependentType.Name} to {principalType.Name} ({columns}) does not include " +
            "TenantId, so the database lets it point at another tenant's row (commits through the unit of work are " +
            "checked; raw SQL and other writers are not). Use a composite key: an alternate key (TenantId, Id) on " +
            $"{principalType.Name} and a foreign key (TenantId, {columns}) on {dependentType.Name}." +
            (required ? "" : " (Warning; RequireTenantScopedForeignKeys makes it an error.)");
    }
}
