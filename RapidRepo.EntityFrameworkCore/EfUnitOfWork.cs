using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using RapidRepo.Auditing;
using RapidRepo.Concurrency;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.EntityFrameworkCore.Internal;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;
using System.Data;
using System.Data.Common;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// An EF Core unit of work: stamps audit fields and concurrency versions on the tracked changes and saves the
/// <see cref="DbContext"/>.
/// </summary>
/// <remarks>
/// The unit of work does not own the context it is given and never disposes it; whoever created the context,
/// usually the dependency injection container, does.
/// </remarks>
/// <typeparam name="TUserKey">The type of the user key recorded in audit fields.</typeparam>
public abstract class EfUnitOfWork<TUserKey> : UnitOfWorkBase<TUserKey>
    where TUserKey : struct
{
    /// <summary>
    /// Set while the execution strategy opens a transaction through <see cref="BeginTransactionCoreAsync"/>, which may
    /// then open one even though the strategy retries on failure. A field is enough: like the <see cref="DbContext"/>,
    /// the unit of work is not thread-safe.
    /// </summary>
    private bool _strategyIsOpeningTransaction;

    protected EfUnitOfWork(DbContext dbContext, TUserKey defaultUserKey)
        : this(dbContext, defaultUserKey, serviceProvider: null, timeProvider: null)
    {
    }

    protected EfUnitOfWork(DbContext dbContext, TUserKey defaultUserKey, IServiceProvider serviceProvider)
        : this(dbContext, defaultUserKey, serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)), timeProvider: null)
    {
    }

    /// <param name="dbContext">The context whose changes this unit of work commits.</param>
    /// <param name="defaultUserKey">The user recorded in audit fields when a commit names none.</param>
    /// <param name="serviceProvider">The service provider custom repositories are resolved from, if any.</param>
    /// <param name="timeProvider">The clock for audit timestamps, or <see langword="null"/> for the system clock.</param>
    protected EfUnitOfWork(
        DbContext dbContext,
        TUserKey defaultUserKey,
        IServiceProvider? serviceProvider,
        TimeProvider? timeProvider)
        : this(dbContext, defaultUserKey, serviceProvider, timeProvider, currentUserProvider: null)
    {
    }

    /// <param name="dbContext">The context whose changes this unit of work commits.</param>
    /// <param name="defaultUserKey">The user recorded in audit fields when neither the commit nor <paramref name="currentUserProvider"/> names one.</param>
    /// <param name="serviceProvider">The service provider custom repositories are resolved from, if any.</param>
    /// <param name="timeProvider">The clock for audit timestamps, or <see langword="null"/> for the system clock.</param>
    /// <param name="currentUserProvider">
    /// Supplies the audit user when a commit names none, or <see langword="null"/> to resolve one from
    /// <paramref name="serviceProvider"/>, if any.
    /// </param>
    protected EfUnitOfWork(
        DbContext dbContext,
        TUserKey defaultUserKey,
        IServiceProvider? serviceProvider,
        TimeProvider? timeProvider,
        ICurrentUserProvider<TUserKey>? currentUserProvider)
        : base(defaultUserKey, serviceProvider, timeProvider, currentUserProvider)
    {
        DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    protected DbContext DbContext { get; }

    protected override IRepository<TEntity, TKey> CreateRepository<TEntity, TKey>() =>
        new EfRepository<TEntity, TKey>(DbContext);

    protected override void CommitCore(TUserKey? userId)
    {
        var versions = new EfVersionTracker();

        try
        {
            StampChanges(userId, versions);
            DbContext.SaveChanges();
        }
        catch (Exception exception)
        {
            if (OnCommitFailed(exception, versions) is { } conflict)
                throw conflict;

            throw;
        }
    }

    protected override async Task CommitCoreAsync(TUserKey? userId, CancellationToken cancellationToken)
    {
        var versions = new EfVersionTracker();

        try
        {
            StampChanges(userId, versions);
            await DbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            if (OnCommitFailed(exception, versions) is { } conflict)
                throw conflict;

            throw;
        }
    }

    /// <summary>
    /// Joins an open transaction, and otherwise opens one. With an execution strategy that retries on failure, such
    /// as <c>EnableRetryOnFailure</c>, a transaction opened by <c>BeginTransactionAsync</c> could not be retried, so it
    /// is refused in favour of <c>ExecuteInTransactionAsync</c>, which opens its transaction through the strategy.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The context's execution strategy retries on failure, or the open transaction runs at an isolation level weaker
    /// than <paramref name="isolationLevel"/>.
    /// </exception>
    protected override async Task<IUnitOfWorkTransaction> BeginTransactionCoreAsync(
        IsolationLevel? isolationLevel,
        CancellationToken cancellationToken)
    {
        var openedByStrategy = _strategyIsOpeningTransaction;
        _strategyIsOpeningTransaction = false;

        if (DbContext.Database.CurrentTransaction is { } openTransaction)
        {
            EnsureJoinable(openTransaction, isolationLevel);
            return JoinedTransaction.Instance;
        }

        if (!openedByStrategy && DbContext.Database.CreateExecutionStrategy().RetriesOnFailure)
        {
            throw new InvalidOperationException(
                "BeginTransactionAsync cannot be used with an execution strategy that retries on failure, such as " +
                "EnableRetryOnFailure: the strategy cannot retry a transaction it did not start. Use " +
                "ExecuteInTransactionAsync instead, which runs the whole transaction through the strategy.");
        }

        var transaction = isolationLevel is { } level
            ? await DbContext.Database.BeginTransactionAsync(level, cancellationToken)
            : await DbContext.Database.BeginTransactionAsync(cancellationToken);

        return new EfUnitOfWorkTransaction(transaction);
    }

    /// <summary>
    /// Runs the transaction through the context's execution strategy, so that a strategy that retries on failure
    /// retries the transaction as a whole. Before a retry, the change tracker is cleared: the entities the failed
    /// attempt tracked would otherwise be saved again.
    /// </summary>
    protected override Task<TResult> ExecuteInTransactionCoreAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel? isolationLevel,
        CancellationToken cancellationToken)
    {
        if (DbContext.Database.CurrentTransaction is not null)
            return base.ExecuteInTransactionCoreAsync(operation, isolationLevel, cancellationToken);

        var isRetry = false;

        return DbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            async ct =>
            {
                if (isRetry)
                    DbContext.ChangeTracker.Clear();

                isRetry = true;
                _strategyIsOpeningTransaction = true;

                try
                {
                    return await base.ExecuteInTransactionCoreAsync(operation, isolationLevel, ct);
                }
                finally
                {
                    _strategyIsOpeningTransaction = false;
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Refuses to join <paramref name="openTransaction"/> when it runs at an isolation level weaker than the one
    /// requested, which would otherwise be ignored silently. A level that can't be read is not checked.
    /// </summary>
    private static void EnsureJoinable(IDbContextTransaction openTransaction, IsolationLevel? requested)
    {
        if (requested is not { } requestedLevel || requestedLevel == IsolationLevel.Unspecified)
            return;

        var openLevel = (openTransaction as IInfrastructure<DbTransaction>)?.Instance.IsolationLevel;

        if (openLevel is not { } open || open == IsolationLevel.Unspecified || IsolationLevels.Satisfies(open, requestedLevel))
            return;

        throw new InvalidOperationException(
            $"A transaction at isolation level {requestedLevel} was requested, but the transaction already open on the " +
            $"context, which it would join, runs at {open}. Open the outer transaction at {requestedLevel} or " +
            "stricter, or request a level it provides.");
    }

    /// <summary>
    /// Undoes the version changes of a failed commit and returns the <see cref="ConcurrencyException"/> to throw in
    /// place of <paramref name="exception"/>, or <see langword="null"/> to rethrow it.
    /// </summary>
    private static ConcurrencyException? OnCommitFailed(Exception exception, EfVersionTracker versions)
    {
        versions.Revert();
        return EfVersionTracker.Translate(exception);
    }

    /// <summary>
    /// Stamps every added or modified entry with one timestamp for the whole commit, and sets up the concurrency
    /// check of every updated or deleted <see cref="IVersionedEntity"/>.
    /// </summary>
    private void StampChanges(TUserKey? userId, EfVersionTracker versions)
    {
        var utcNow = Auditor.UtcNow;

        foreach (var entry in DbContext.ChangeTracker.Entries())
        {
            if (Classify(entry) is { } kind)
            {
                Auditor.Stamp(entry.Entity, kind, userId, utcNow);
            }

            versions.Stamp(entry);
        }
    }

    /// <summary>
    /// Maps a change tracker entry to the kind of change it represents, or <see langword="null"/> when it is not
    /// stamped (unchanged, detached or hard-deleted).
    /// </summary>
    /// <remarks>
    /// A soft delete or restore is recognised by <see cref="EntityChangeClassifier.ClassifyUpdate"/>, from the
    /// original <c>DeletedAt</c>. Only the transition counts, so saving an already deleted entity again does not
    /// overwrite who deleted it and when.
    /// </remarks>
    private static EntityChangeKind? Classify(EntityEntry entry)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                return EntityChangeKind.Added;

            case EntityState.Modified when entry.Entity is IDeletableEntity
                && entry.Metadata.FindProperty(nameof(IDeletableEntity.DeletedAt)) is not null:
                var originalDeletedAt = (DateTime?)entry.Property(nameof(IDeletableEntity.DeletedAt)).OriginalValue;

                return EntityChangeClassifier.ClassifyUpdate(entry.Entity, originalDeletedAt);

            case EntityState.Modified:
                return EntityChangeKind.Modified;

            default:
                return null;
        }
    }
}
