using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Compatibility;

namespace RapidRepo.UnitOfWork;

/// <summary>The 7.x name of <see cref="EfUnitOfWork{TUserKey}"/>.</summary>
[Obsolete(Obsoletions.UnitOfWork, DiagnosticId = Obsoletions.DiagnosticId)]
public abstract class UnitOfWork<TUserKey> : EfUnitOfWork<TUserKey>
    where TUserKey : struct
{
    protected UnitOfWork(DbContext dbContext, TUserKey defaultUserKey)
        : base(dbContext, defaultUserKey)
    {
    }

    protected UnitOfWork(DbContext dbContext, TUserKey defaultUserKey, IServiceProvider serviceProvider)
        : base(dbContext, defaultUserKey, serviceProvider)
    {
    }
}
