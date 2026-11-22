using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using RapidRepo.Entities.Interfaces;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// Marks the <see cref="IVersionedEntity.Version"/> property of every entity type that implements
/// <see cref="IVersionedEntity"/> as a concurrency token, once the model is complete.
/// </summary>
/// <remarks>
/// Running when the model is finalized, rather than in <c>OnModelCreating</c>, makes it independent of the order in
/// which entity types are configured. As a convention it yields to explicit configuration, so
/// <c>Property(e =&gt; e.Version).IsConcurrencyToken(false)</c> still wins (and the unit of work then refuses to
/// commit the entity).
/// </remarks>
internal sealed class ConcurrencyTokenConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (!typeof(IVersionedEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            // Declared on the type that owns the property, so a hierarchy configures it once.
            entityType.FindDeclaredProperty(nameof(IVersionedEntity.Version))?.Builder.IsConcurrencyToken(true);
        }
    }
}
