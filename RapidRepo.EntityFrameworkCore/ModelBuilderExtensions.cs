using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RapidRepo.Entities.Interfaces;
using System.Linq.Expressions;

namespace RapidRepo.Extensions;

/// <summary>
/// Extension methods for <see cref="ModelBuilder"/> to simplify common configuration patterns.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// The name of the soft-delete query filter that <see cref="AddSoftDeleteQueryFilters"/> registers. Pass it to
    /// <c>IgnoreQueryFilters([...])</c> to bypass soft delete alone in a hand-written query.
    /// </summary>
    public const string SoftDeleteFilterKey = "RapidRepo.SoftDelete";

    /// <summary>
    /// Applies a global query filter on all entity types that implement <see cref="IDeletableEntity"/>
    /// so that soft-deleted records are excluded from queries by default.
    /// Call this from <c>OnModelCreating</c> in your <see cref="DbContext"/>, after the entity types are configured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The filter is a named filter (<see cref="SoftDeleteFilterKey"/>), so it can sit next to other named filters,
    /// such as a tenant filter, and <c>ReadOptions.IncludeDeleted</c> bypasses it alone.
    /// </para>
    /// <para>
    /// EF Core does not allow anonymous and named filters on the same entity type, so an entity type that already
    /// has an anonymous filter gets an anonymous soft-delete filter instead, which replaces the existing one as in
    /// RapidRepo 8.x. For such an entity, <c>IncludeDeleted</c> bypasses every filter.
    /// </para>
    /// <para>
    /// The filter is declared on the root of each inheritance hierarchy and covers its derived types, so the root
    /// must implement <see cref="IDeletableEntity"/> whenever any type in the hierarchy does.
    /// </para>
    /// </remarks>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same model builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// A type implements <see cref="IDeletableEntity"/> but the root of its inheritance hierarchy does not.
    /// </exception>
    public static ModelBuilder AddSoftDeleteQueryFilters(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!IsDeletable(entityType))
                continue;

            // Query filters may only be declared on the root of an inheritance hierarchy. A deletable root's
            // filter covers its derived types; a deletable type under a root that is not deletable cannot be
            // filtered, and skipping it would silently return its soft-deleted rows.
            if (entityType.BaseType is not null)
            {
                EnsureRootIsDeletable(entityType);
                continue;
            }

            var filter = BuildIsNotDeletedFilter(entityType.ClrType);
            var entity = modelBuilder.Entity(entityType.ClrType);

            if (HasAnonymousFilter(entityType))
                entity.HasQueryFilter(filter);
            else
                entity.HasQueryFilter(SoftDeleteFilterKey, filter);
        }

        return modelBuilder;
    }

    private static LambdaExpression BuildIsNotDeletedFilter(Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var deletedAtProperty = Expression.Property(parameter, nameof(IDeletableEntity.DeletedAt));
        var nullConstant = Expression.Constant(null, typeof(DateTime?));
        var isNotDeleted = Expression.Equal(deletedAtProperty, nullConstant);
        return Expression.Lambda(isNotDeleted, parameter);
    }

    private static bool IsDeletable(IMutableEntityType entityType) =>
        typeof(IDeletableEntity).IsAssignableFrom(entityType.ClrType);

    private static void EnsureRootIsDeletable(IMutableEntityType entityType)
    {
        var root = entityType.GetRootType();
        if (IsDeletable(root))
            return;

        throw new InvalidOperationException(
            $"'{entityType.ClrType.Name}' implements '{nameof(IDeletableEntity)}', but the root of its inheritance " +
            $"hierarchy, '{root.ClrType.Name}', does not. EF Core only allows query filters on the root entity " +
            $"type, so {nameof(AddSoftDeleteQueryFilters)} cannot hide soft-deleted '{entityType.ClrType.Name}' " +
            $"rows. Implement '{nameof(IDeletableEntity)}' on '{root.ClrType.Name}', or map " +
            $"'{entityType.ClrType.Name}' outside that hierarchy.");
    }

    private static bool HasAnonymousFilter(IMutableEntityType entityType) =>
        entityType.GetDeclaredQueryFilters().Any(f => f.IsAnonymous);
}
