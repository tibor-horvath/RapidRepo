using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Compatibility;

namespace RapidRepo.Repositories;

/// <summary>The 8.x concrete write repository that <c>BaseRepository</c> composed.</summary>
[Obsolete(Obsoletions.BaseWriteRepository, DiagnosticId = Obsoletions.DiagnosticId)]
public sealed class BaseWriteRepository<TEntity, TId>(DbContext dbContext)
    : EfWriteRepository<TEntity, TId>(dbContext)
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
}
