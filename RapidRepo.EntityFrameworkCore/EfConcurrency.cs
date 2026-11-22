using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RapidRepo.Concurrency;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Extensions;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// The EF Core side of optimistic concurrency: finding the <c>Version</c> concurrency token, and translating EF Core's
/// conflict. Stamping and reverting versions is the core commit sequence's job (see <see cref="EfStagedChange"/>).
/// </summary>
internal static class EfConcurrency
{
    /// <summary>
    /// Translates EF Core's conflict into the provider-neutral <see cref="ConcurrencyException"/> when it concerns an
    /// <see cref="IVersionedEntity"/>.
    /// </summary>
    /// <remarks>
    /// A conflict that involves only other entities, for example an update of a row that was deleted, or a
    /// concurrency token the application configured itself, is left as EF Core's
    /// <see cref="DbUpdateConcurrencyException"/>, as before entities could opt in.
    /// </remarks>
    public static ConcurrencyException? Translate(Exception exception) =>
        exception is DbUpdateConcurrencyException concurrency
        && concurrency.Entries.Any(e => e.Entity is IVersionedEntity)
            ? new ConcurrencyException(concurrency.Entries.Select(e => e.Entity), concurrency)
            : null;

    /// <summary>
    /// Finds the <c>Version</c> property and checks that EF Core compares it on update and delete. Without that, the
    /// version would be incremented but never checked, and conflicting writes would be lost silently.
    /// </summary>
    public static IProperty GetVersionProperty(IEntityType entityType)
    {
        var property = entityType.FindProperty(nameof(IVersionedEntity.Version));
        if (property is { IsConcurrencyToken: true })
            return property;

        throw new InvalidOperationException(
            $"'{entityType.ClrType.Name}' implements '{nameof(IVersionedEntity)}', but its " +
            $"'{nameof(IVersionedEntity.Version)}' property is not mapped as a concurrency token, so conflicting " +
            $"changes would be lost silently. Call configurationBuilder." +
            $"{nameof(ModelConfigurationBuilderExtensions.AddConcurrencyTokens)}() in ConfigureConventions.");
    }
}
