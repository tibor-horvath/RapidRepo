using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Compatibility;

namespace RapidRepo.Repositories;

/// <summary>The 7.x name of <see cref="EfReadOnlyRepository{TEntity, TId}"/>.</summary>
[Obsolete(Obsoletions.ReadOnlyRepository, DiagnosticId = Obsoletions.DiagnosticId)]
public abstract class ReadOnlyRepository<TEntity, TId>(DbContext dbContext)
    : EfReadOnlyRepository<TEntity, TId>(dbContext)
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
}
