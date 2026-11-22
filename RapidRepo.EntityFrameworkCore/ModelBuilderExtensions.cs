using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RapidRepo.Entities.Interfaces;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Internal;
using RapidRepo.Tenancy;
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

    /// <summary>
    /// The name of the tenant query filter that <see cref="AddTenantQueryFilters{TTenantKey}"/> registers.
    /// </summary>
    public const string TenantFilterKey = "RapidRepo.Tenant";

    /// <summary>
    /// The model annotation that records <see cref="TenantQueryFilterOptions.RequireTenantScopedForeignKeys"/>.
    /// </summary>
    internal const string RequireTenantScopedForeignKeysAnnotation = "RapidRepo:RequireTenantScopedForeignKeys";

    /// <summary>
    /// Applies the tenant query filter to every entity type that implements <see cref="ITenantEntity{TTenantKey}"/>,
    /// so that reads return only the current tenant's rows. Call it from <c>OnModelCreating</c>, passing the context
    /// itself, after the entity types are configured:
    /// <code>modelBuilder.AddTenantQueryFilters(this);</code>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The filter is a named filter (<see cref="TenantFilterKey"/>), so it sits next to the soft-delete filter and
    /// <c>ReadOptions.IncludeDeleted</c> never lifts it. It reads the tenant on every query, from the innermost
    /// <c>ITenantScope&lt;TTenantKey&gt;.Override</c> or else <see cref="ITenantDbContext{TTenantKey}.CurrentTenantId"/>,
    /// and matches every row inside a host scope. Without a current tenant it matches no rows.
    /// </para>
    /// <para>
    /// It also maps <c>TenantId</c> as a concurrency token, so every update and delete of a tenant entity is written
    /// only <c>WHERE TenantId</c> still holds the tenant the commit checked. A row moved to another tenant between the
    /// check and the write is left alone, and EF Core reports it with a <see cref="DbUpdateConcurrencyException"/>,
    /// as it does for a row that no longer exists. This adds no column and needs no migration.
    /// </para>
    /// <para>
    /// Unlike <see cref="AddSoftDeleteQueryFilters"/>, it never replaces an existing anonymous filter, which would
    /// silently drop the application's own tenant condition: it throws instead. Remove the tenant condition from that
    /// filter, and turn the rest of it into a named filter.
    /// </para>
    /// <para>
    /// The filter is declared on the root of each inheritance hierarchy, so the root must implement
    /// <see cref="ITenantEntity{TTenantKey}"/> whenever any type in the hierarchy does.
    /// </para>
    /// </remarks>
    /// <typeparam name="TTenantKey">The tenant key type of every tenant entity in the model.</typeparam>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="context">The context being configured, usually <see langword="this"/>.</param>
    /// <param name="configure">Configures the tenancy checks, or <see langword="null"/> for the defaults.</param>
    /// <returns>The same model builder, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="context"/> is not a <see cref="DbContext"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// A tenant entity already has an anonymous query filter; uses a different tenant key type; or derives from a
    /// root that is not a tenant entity.
    /// </exception>
    public static ModelBuilder AddTenantQueryFilters<TTenantKey>(
        this ModelBuilder modelBuilder,
        ITenantDbContext<TTenantKey> context,
        Action<TenantQueryFilterOptions>? configure = null)
        where TTenantKey : notnull
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(context);

        // The parameter is typed as ITenantDbContext<TTenantKey> so that TTenantKey is inferred from 'this'; a
        // TContext : DbContext, ITenantDbContext<TTenantKey> constraint could not infer it. Hence the run-time check.
        if (context is not DbContext)
        {
            throw new ArgumentException(
                $"Pass the DbContext being configured ('this'), not another {nameof(ITenantDbContext<TTenantKey>)}.",
                nameof(context));
        }

        var filterOptions = new TenantQueryFilterOptions();
        configure?.Invoke(filterOptions);
        modelBuilder.Model.SetAnnotation(RequireTenantScopedForeignKeysAnnotation, filterOptions.RequireTenantScopedForeignKeys);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var keyType = TenantTraits.GetTenantKeyType(entityType.ClrType);
            if (keyType is null)
                continue;

            if (keyType != typeof(TTenantKey))
                throw new InvalidOperationException(TenancyMessages.KeyTypeMismatch(entityType.ClrType, keyType, typeof(TTenantKey)));

            if (entityType.BaseType is not null)
            {
                EnsureRootIsTenantEntity(entityType);
                continue;
            }

            if (HasAnonymousFilter(entityType))
                throw new InvalidOperationException(TenancyMessages.AnonymousFilter(entityType.ClrType));

            var entity = modelBuilder.Entity(entityType.ClrType);
            entity.HasQueryFilter(TenantFilterKey, BuildTenantFilter<TTenantKey>(entityType.ClrType, context));

            // The commit checks the stored tenant before it writes; comparing TenantId in the UPDATE or DELETE itself
            // makes that check hold until the write, so a row moved to another tenant in between is not written.
            entity.Property(TenantTraits.TenantIdPropertyName).IsConcurrencyToken();
        }

        return modelBuilder;
    }

    /// <summary>
    /// <c>e =&gt; TenantQueryFilter.IsHostScope(context) || e.TenantId == TenantQueryFilter.CurrentTenantId(context)</c>.
    /// Both values are read through the context, which EF Core turns into query parameters evaluated per query.
    /// </summary>
    private static LambdaExpression BuildTenantFilter<TTenantKey>(Type clrType, ITenantDbContext<TTenantKey> context)
        where TTenantKey : notnull
    {
        var parameter = Expression.Parameter(clrType, "e");
        var contextExpression = Expression.Convert(Expression.Constant(context), typeof(ITenantDbContext<TTenantKey>));

        var comparisonType = typeof(TTenantKey).IsValueType
            ? typeof(Nullable<>).MakeGenericType(typeof(TTenantKey))
            : typeof(TTenantKey);

        var tenantId = Expression.Convert(
            Expression.Property(parameter, TenantTraits.TenantIdPropertyName),
            comparisonType);

        var currentTenantId = Expression.Convert(
            Expression.Call(
                typeof(TenantQueryFilter),
                nameof(TenantQueryFilter.CurrentTenantId),
                [typeof(TTenantKey)],
                contextExpression),
            comparisonType);

        var isHostScope = Expression.Call(
            typeof(TenantQueryFilter),
            nameof(TenantQueryFilter.IsHostScope),
            [typeof(TTenantKey)],
            contextExpression);

        return Expression.Lambda(Expression.OrElse(isHostScope, Expression.Equal(tenantId, currentTenantId)), parameter);
    }

    private static void EnsureRootIsTenantEntity(IMutableEntityType entityType)
    {
        var root = entityType.GetRootType();
        if (!TenantTraits.IsTenantEntity(root.ClrType))
            throw new InvalidOperationException(TenancyMessages.RootNotTenantEntity(entityType.ClrType, root.ClrType));
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
