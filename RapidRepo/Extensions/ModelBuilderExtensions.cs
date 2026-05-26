using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities.Interfaces;
using System.Linq.Expressions;
using System.Reflection;

namespace RapidRepo.Extensions;

/// <summary>
/// Extension methods for <see cref="ModelBuilder"/> to simplify common configuration patterns.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies a global query filter on all entity types that implement <see cref="IDeletableEntity"/>
    /// so that soft-deleted records are excluded from queries by default.
    /// Call this from <c>OnModelCreating</c> in your <see cref="DbContext"/>.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    public static ModelBuilder AddSoftDeleteQueryFilters(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IDeletableEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var deletedAtProperty = Expression.Property(parameter, nameof(IDeletableEntity.DeletedAt));
            var nullConstant = Expression.Constant(null, typeof(DateTime?));
            var isNotDeleted = Expression.Equal(deletedAtProperty, nullConstant);
            var lambda = Expression.Lambda(isNotDeleted, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }

        return modelBuilder;
    }
}
