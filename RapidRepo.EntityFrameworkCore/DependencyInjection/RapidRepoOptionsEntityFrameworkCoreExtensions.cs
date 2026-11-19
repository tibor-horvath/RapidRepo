using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.DependencyInjection;
using RapidRepo.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Plugs the EF Core provider into <see cref="RapidRepoServiceCollectionExtensions.AddRapidRepo"/>.
/// </summary>
public static class RapidRepoOptionsEntityFrameworkCoreExtensions
{
    /// <summary>
    /// Use EF Core for this <c>AddRapidRepo</c> call, bound to <typeparamref name="TContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>AddDbContext&lt;TContext&gt;()</c> registers <typeparamref name="TContext"/> but not the base
    /// <see cref="DbContext"/> type, so repositories and units of work that take a <see cref="DbContext"/>
    /// constructor parameter cannot be activated by the container on their own. Binding the call to the context
    /// registers the missing <see cref="DbContext"/> → <typeparamref name="TContext"/> forwarder, and only when
    /// something registered by this call actually needs it.
    /// </para>
    /// <para>
    /// With <see cref="RapidRepoOptions.RegisterGenericRepositories"/>, it registers a generic repository per
    /// <see cref="DbSet{TEntity}"/> on <typeparamref name="TContext"/>, each bound to <typeparamref name="TContext"/>,
    /// instead of one open-generic fallback. That is what lets several contexts coexist.
    /// </para>
    /// <para>Replaces <c>UseDbContext&lt;TContext&gt;()</c> from RapidRepo 8.x.</para>
    /// </remarks>
    /// <typeparam name="TContext">The context the repositories of this call use.</typeparam>
    /// <param name="options">The <c>AddRapidRepo</c> options.</param>
    /// <param name="configure">Configures the provider, or <see langword="null"/> for the defaults.</param>
    /// <returns>The same options, for chaining.</returns>
    /// <exception cref="InvalidOperationException">A provider has already been configured for this call.</exception>
    public static RapidRepoOptions UseEntityFrameworkCore<TContext>(
        this RapidRepoOptions options,
        Action<EntityFrameworkCoreProviderOptions>? configure = null)
        where TContext : DbContext
        => options.UseEntityFrameworkCore(typeof(TContext), configure);

    /// <summary>
    /// Use EF Core for this <c>AddRapidRepo</c> call without binding it to a context.
    /// </summary>
    /// <remarks>
    /// Repositories taking a base <see cref="DbContext"/> then rely on the application registering
    /// <see cref="DbContext"/> itself; RapidRepo warns (or, with
    /// <see cref="EntityFrameworkCoreProviderOptions.ThrowOnMissingDbContext"/>, throws) when nothing does. With
    /// <see cref="RapidRepoOptions.RegisterGenericRepositories"/>, it registers <see cref="RapidRepo.EntityFrameworkCore.EfRepository{TEntity, TId}"/>
    /// as an open-generic fallback.
    /// </remarks>
    /// <param name="options">The <c>AddRapidRepo</c> options.</param>
    /// <param name="configure">Configures the provider, or <see langword="null"/> for the defaults.</param>
    /// <returns>The same options, for chaining.</returns>
    /// <exception cref="InvalidOperationException">A provider has already been configured for this call.</exception>
    public static RapidRepoOptions UseEntityFrameworkCore(
        this RapidRepoOptions options,
        Action<EntityFrameworkCoreProviderOptions>? configure = null)
        => options.UseEntityFrameworkCore(contextType: null, configure);

    private static RapidRepoOptions UseEntityFrameworkCore(
        this RapidRepoOptions options,
        Type? contextType,
        Action<EntityFrameworkCoreProviderOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(options);

        var providerOptions = new EntityFrameworkCoreProviderOptions();
        configure?.Invoke(providerOptions);

        return options.UseProvider(new EntityFrameworkCoreProvider(contextType, providerOptions));
    }
}
