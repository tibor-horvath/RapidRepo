using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Compatibility;

namespace RapidRepo.Repositories;

/// <summary>The 7.x name of <see cref="EfWriteRepository{TEntity, TId}"/>.</summary>
[Obsolete(Obsoletions.WriteRepository, DiagnosticId = Obsoletions.DiagnosticId)]
public abstract class WriteRepository<TEntity, TId>(DbContext dbContext)
    : EfWriteRepository<TEntity, TId>(dbContext)
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
}
