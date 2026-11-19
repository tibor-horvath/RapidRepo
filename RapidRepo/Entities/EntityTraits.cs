using RapidRepo.Entities.Interfaces;
using System.Linq.Expressions;

namespace RapidRepo.Entities;

/// <summary>
/// Facts about <typeparamref name="TEntity"/> that soft delete and restore depend on, resolved once per closed
/// entity type and shared by every provider.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public static class EntityTraits<TEntity>
    where TEntity : class
{
    private static readonly Action<TEntity>? ClearDeletedByAction = BuildClearDeletedBy();

    /// <summary>
    /// Gets a value indicating whether <typeparamref name="TEntity"/> implements <see cref="IDeletableEntity"/>, so
    /// that <c>Delete</c> marks it as deleted rather than removing it.
    /// </summary>
    public static bool SupportsSoftDelete { get; } = typeof(IDeletableEntity).IsAssignableFrom(typeof(TEntity));

    /// <summary>
    /// Gets a value indicating whether <typeparamref name="TEntity"/> records who deleted it, through
    /// <see cref="IDeletableEntity{TUserKey}.DeletedBy"/>.
    /// </summary>
    public static bool TracksDeletedBy => ClearDeletedByAction is not null;

    /// <summary>
    /// Clears <see cref="IDeletableEntity{TUserKey}.DeletedBy"/>, whatever the user key type. Does nothing when
    /// <typeparamref name="TEntity"/> does not track who deleted it.
    /// </summary>
    /// <remarks>
    /// The user key type is not known to a repository, so the property cannot be reached through a cast the way
    /// <see cref="IDeletableEntity.DeletedAt"/> can. The setter is compiled once instead of reflected per call.
    /// </remarks>
    /// <param name="entity">The entity.</param>
    public static void ClearDeletedBy(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ClearDeletedByAction?.Invoke(entity);
    }

    private static Action<TEntity>? BuildClearDeletedBy()
    {
        var deletableInterfaces = typeof(TEntity)
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDeletableEntity<>))
            .ToList();

        if (deletableInterfaces.Count == 0)
            return null;

        var entity = Expression.Parameter(typeof(TEntity), "entity");
        var assignments = deletableInterfaces.Select(i =>
        {
            var property = i.GetProperty(nameof(IDeletableEntity<int>.DeletedBy))!;
            return (Expression)Expression.Assign(
                Expression.Property(Expression.Convert(entity, i), property),
                Expression.Default(property.PropertyType));
        });

        return Expression.Lambda<Action<TEntity>>(Expression.Block(assignments), entity).Compile();
    }
}
