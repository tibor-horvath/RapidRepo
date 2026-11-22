using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Compatibility;

namespace RapidRepo.UnitOfWork;

/// <summary>The 8.x name of <see cref="EfUnitOfWork{TUserKey}"/>.</summary>
/// <remarks>
/// Still implements <see cref="IDisposable"/> so that 8.x unit of work interfaces declared with
/// <c>: IDisposable</c> keep compiling. <see cref="Dispose"/> no longer disposes the <c>DbContext</c>, which the
/// unit of work never owned; the container that created it disposes it.
/// </remarks>
[Obsolete(Obsoletions.UnitOfWork, DiagnosticId = Obsoletions.DiagnosticId)]
public abstract class UnitOfWork<TUserKey> : EfUnitOfWork<TUserKey>, IDisposable
    where TUserKey : struct
{
    /// <summary>Creates a unit of work that commits the changes tracked by <paramref name="dbContext"/>.</summary>
    /// <param name="dbContext">The context whose changes this unit of work commits.</param>
    /// <param name="defaultUserKey">The user recorded in audit fields when a commit names none.</param>
    protected UnitOfWork(DbContext dbContext, TUserKey defaultUserKey)
        : base(dbContext, defaultUserKey)
    {
    }

    /// <summary>Creates a unit of work that commits the changes tracked by <paramref name="dbContext"/>.</summary>
    /// <param name="dbContext">The context whose changes this unit of work commits.</param>
    /// <param name="defaultUserKey">The user recorded in audit fields when a commit names none.</param>
    /// <param name="serviceProvider">The service provider custom repositories are resolved from.</param>
    protected UnitOfWork(DbContext dbContext, TUserKey defaultUserKey, IServiceProvider serviceProvider)
        : base(dbContext, defaultUserKey, serviceProvider)
    {
    }

    /// <summary>Does nothing; kept for source compatibility with 8.x.</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
