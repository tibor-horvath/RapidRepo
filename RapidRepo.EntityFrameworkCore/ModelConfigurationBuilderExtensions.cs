using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities.Interfaces;
using RapidRepo.EntityFrameworkCore;

namespace RapidRepo.Extensions;

/// <summary>
/// Extension methods for <see cref="ModelConfigurationBuilder"/> that register RapidRepo's model conventions.
/// </summary>
public static class ModelConfigurationBuilderExtensions
{
    /// <summary>
    /// Marks the <see cref="IVersionedEntity.Version"/> property of every entity type that implements
    /// <see cref="IVersionedEntity"/> as a concurrency token, so that an update or delete only applies while the row
    /// still has the version the entity was read at. Call this from <c>ConfigureConventions</c> in your
    /// <see cref="DbContext"/>; it applies to every entity type, whenever it is configured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The version is a plain <see cref="long"/> column that the unit of work increments, so it behaves the same on
    /// every database. It is not a <c>rowversion</c> or <c>xmin</c> column, and the database does not change it.
    /// </para>
    /// <para>
    /// A commit that writes an <see cref="IVersionedEntity"/> whose <c>Version</c> is not a concurrency token throws
    /// <see cref="InvalidOperationException"/>, so forgetting this call cannot lose updates silently. Marking the
    /// property yourself, for example with <c>[ConcurrencyCheck]</c>, works too.
    /// </para>
    /// </remarks>
    /// <param name="configurationBuilder">The model configuration builder.</param>
    /// <returns>The same configuration builder, for chaining.</returns>
    public static ModelConfigurationBuilder AddConcurrencyTokens(this ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Conventions.Add(_ => new ConcurrencyTokenConvention());
        return configurationBuilder;
    }
}
