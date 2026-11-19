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
    /// </remarks>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same model builder, for chaining.</returns>
    public static ModelBuilder AddSoftDeleteQueryFilters(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Query filters may only be declared on the root of an inheritance hierarchy.
            if (entityType.BaseType is not null || !typeof(IDeletableEntity).IsAssignableFrom(entityType.ClrType))
                continue;

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

    private static bool HasAnonymousFilter(IMutableEntityType entityType) =>
        entityType.GetDeclaredQueryFilters().Any(f => f.IsAnonymous);
}
