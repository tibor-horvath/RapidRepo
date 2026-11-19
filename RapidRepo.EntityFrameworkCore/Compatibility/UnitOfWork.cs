using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Compatibility;

namespace RapidRepo.UnitOfWork;

/// <summary>The 7.x name of <see cref="EfUnitOfWork{TUserKey}"/>.</summary>
/// <remarks>
/// Still implements <see cref="IDisposable"/> so that 7.x unit of work interfaces declared with
/// <c>: IDisposable</c> keep compiling. <see cref="Dispose"/> no longer disposes the <c>DbContext</c>, which the
/// unit of work never owned; the container that created it disposes it.
/// </remarks>
[Obsolete(Obsoletions.UnitOfWork, DiagnosticId = Obsoletions.DiagnosticId)]
public abstract class UnitOfWork<TUserKey> : EfUnitOfWork<TUserKey>, IDisposable
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

    /// <summary>Does nothing; kept for source compatibility with 7.x.</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
