using Microsoft.EntityFrameworkCore;
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

    /// <summary>Creates a unit of work that commits the changes tracked by <paramref name="dbContext"/>.</summary>
    /// <param name="dbContext">The context whose changes this unit of work commits.</param>
    /// <param name="defaultUserKey">The user recorded in audit fields when a commit names none.</param>
    protected EfUnitOfWork(DbContext dbContext, TUserKey defaultUserKey)
        : this(dbContext, defaultUserKey, serviceProvider: null, timeProvider: null)
    {
    }

    /// <summary>Creates a unit of work that commits the changes tracked by <paramref name="dbContext"/>.</summary>
    /// <param name="dbContext">The context whose changes this unit of work commits.</param>
    /// <param name="defaultUserKey">The user recorded in audit fields when a commit names none.</param>
    /// <param name="serviceProvider">The service provider custom repositories are resolved from.</param>
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

    /// <summary>Gets the <see cref="Microsoft.EntityFrameworkCore.DbContext"/> whose changes this unit of work commits.</summary>
    protected DbContext DbContext { get; }

    /// <inheritdoc />
    protected override IRepository<TEntity, TKey> CreateRepository<TEntity, TKey>() =>
        new EfRepository<TEntity, TKey>(DbContext);

    /// <summary>
    /// Applies the tenant rules to the context's staged changes, querying the database for the tenant stored in
    /// updated and deleted rows and in rows that foreign keys point at.
    /// </summary>
    protected override void EnforceTenantRules() =>
        // Synchronous all the way down: async: false makes the task complete before it is returned.
        EfTenancyEnforcer.EnforceAsync(DbContext, CreateTenantGuard, async: false, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc cref="EnforceTenantRules"/>
    protected override Task EnforceTenantRulesAsync(CancellationToken cancellationToken) =>
        EfTenancyEnforcer.EnforceAsync(DbContext, CreateTenantGuard, async: true, cancellationToken);

    /// <summary>
    /// Gets every added, modified or deleted entry of the context. Reading the entries detects changes, so edits to
    /// tracked entities made without calling <c>Update</c> are committed too.
    /// </summary>
    protected override IReadOnlyList<StagedChange> GetStagedChanges() => EfStagedChange.From(DbContext);

    /// <summary>Saves the context.</summary>
    protected override void PersistStagedChanges() => DbContext.SaveChanges();

    /// <summary>Saves the context.</summary>
    protected override Task PersistStagedChangesAsync(CancellationToken cancellationToken) =>
        DbContext.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// Translates EF Core's <see cref="DbUpdateConcurrencyException"/> into a <see cref="ConcurrencyException"/> when it
    /// concerns an <see cref="IVersionedEntity"/>.
    /// </summary>
    protected override Exception? TranslateCommitFailure(Exception exception) => EfConcurrency.Translate(exception);

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
    /// Reads the current tenant from the context when it implements <see cref="ITenantDbContext{TTenantKey}"/>, so
    /// reads and commits agree on the tenant; otherwise from the registered tenant provider.
    /// </summary>
    protected override object? GetCurrentTenantId(Type tenantKeyType)
    {
        ArgumentNullException.ThrowIfNull(tenantKeyType);

        return TenantDbContexts.TryGetContextTenantId(DbContext, tenantKeyType, out var tenantId)
            ? tenantId
            : base.GetCurrentTenantId(tenantKeyType);
    }

    /// <summary>
    /// Names the context's <c>CurrentTenantId</c> when the context supplies the tenant.
    /// </summary>
    protected override string GetTenantSourceName(Type tenantKeyType)
    {
        ArgumentNullException.ThrowIfNull(tenantKeyType);

        // A type check only: reading CurrentTenantId here would run the tenant provider even when an override
        // applies, where the provider may legitimately have nothing to answer (a background job).
        return TenantDbContexts.Implements(DbContext, tenantKeyType)
            ? TenantDbContexts.SourceName(DbContext)
            : base.GetTenantSourceName(tenantKeyType);
    }
}
