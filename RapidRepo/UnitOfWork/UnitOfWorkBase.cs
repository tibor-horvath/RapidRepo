using RapidRepo.Auditing;
using RapidRepo.Concurrency;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.Tenancy;
using System.Data;

namespace RapidRepo.UnitOfWork;

/// <summary>
/// The provider-neutral base of a unit of work: commit entry points, the commit sequence (tenant rules, audit
/// stamping, optimistic concurrency), and the repository accessors that <c>[GenerateUnitOfWork]</c> emits code
/// against.
/// </summary>
/// <remarks>
/// Providers derive from this class, for example <c>EfUnitOfWork&lt;TUserKey&gt;</c> in
/// RapidRepo.EntityFrameworkCore. A provider supplies its staged changes as <see cref="StagedChange"/>s and
/// persists them; the order of the steps in between lives here (see <see cref="CommitCore"/>).
/// </remarks>
/// <typeparam name="TUserKey">The type of the user key recorded in audit fields.</typeparam>
public abstract class UnitOfWorkBase<TUserKey> : IUnitOfWork<TUserKey>
    where TUserKey : struct
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly AuditUserResolver<TUserKey> _auditUser;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitOfWorkBase{TUserKey}"/> class.
    /// </summary>
    /// <param name="defaultUserKey">The user recorded in audit fields when a commit names none.</param>
    /// <param name="serviceProvider">
    /// The service provider that <see cref="ResolveRepository{TRepo}"/> resolves custom repositories from, or
    /// <see langword="null"/> when none are needed. An <see cref="ICurrentUserProvider{TUserKey}"/> and
    /// <see cref="CurrentUserOptions"/> registered in it are used too.
    /// </param>
    /// <param name="timeProvider">The clock for audit timestamps, or <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    protected UnitOfWorkBase(TUserKey defaultUserKey, IServiceProvider? serviceProvider = null, TimeProvider? timeProvider = null)
        : this(defaultUserKey, serviceProvider, timeProvider, currentUserProvider: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitOfWorkBase{TUserKey}"/> class with a provider for the
    /// current user.
    /// </summary>
    /// <param name="defaultUserKey">The user recorded in audit fields when neither the commit nor the current-user provider names one.</param>
    /// <param name="serviceProvider">
    /// The service provider that <see cref="ResolveRepository{TRepo}"/> resolves custom repositories from, or
    /// <see langword="null"/> when none are needed. <see cref="CurrentUserOptions"/> registered in it are used too.
    /// </param>
    /// <param name="timeProvider">The clock for audit timestamps, or <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    /// <param name="currentUserProvider">
    /// Supplies the audit user when a commit names none, or <see langword="null"/> to resolve one from
    /// <paramref name="serviceProvider"/>, if any.
    /// </param>
    protected UnitOfWorkBase(
        TUserKey defaultUserKey,
        IServiceProvider? serviceProvider,
        TimeProvider? timeProvider,
        ICurrentUserProvider<TUserKey>? currentUserProvider)
    {
        _serviceProvider = serviceProvider;
        Auditor = new AuditStamper<TUserKey>(defaultUserKey, timeProvider);
        _auditUser = new AuditUserResolver<TUserKey>(serviceProvider, currentUserProvider);

        // Strict mode is checked where a user field is stamped, so a commit that stamps none never needs a user.
        // Through the virtual RequireCurrentUser, so a derived unit of work's override applies.
        Auditor.OnMissingUser = () => RequireCurrentUser ? _auditUser.MissingUserException() : null;
    }

    /// <summary>
    /// Gets the audit stamper the provider applies to each changed entity at commit time.
    /// </summary>
    protected AuditStamper<TUserKey> Auditor { get; }

    /// <summary>
    /// Gets the provider asked for the audit user when a commit names none, or <see langword="null"/> when there
    /// is none. It is the one passed to the constructor, otherwise the one registered in the service provider.
    /// </summary>
    protected ICurrentUserProvider<TUserKey>? CurrentUserProvider => _auditUser.CurrentUserProvider;

    /// <summary>
    /// Gets a value indicating whether a commit without a user throws instead of stamping the default user key into
    /// a user field (<c>CreatedBy</c>, <c>ModifiedBy</c>, <c>DeletedBy</c>). A commit that stamps no user field,
    /// for example one that changes only entities without user audit fields, does not need a user. By default it
    /// comes from <see cref="CurrentUserOptions"/> in the service provider, and is <see langword="false"/> without one.
    /// </summary>
    protected virtual bool RequireCurrentUser => _auditUser.RequireCurrentUser;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// <see cref="RequireCurrentUser"/> is set, a user field is to be stamped, and neither <paramref name="userId"/>
    /// nor the current-user provider supplies a user. Nothing is persisted.
    /// </exception>
    public void Commit(TUserKey? userId = null) => CommitCore(_auditUser.Resolve(userId));

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// <see cref="RequireCurrentUser"/> is set, a user field is to be stamped, and neither <paramref name="userId"/>
    /// nor the current-user provider supplies a user. Nothing is persisted.
    /// </exception>
    public Task CommitAsync(TUserKey? userId = null, CancellationToken cancellationToken = default) =>
        CommitCoreAsync(_auditUser.Resolve(userId), cancellationToken);

    /// <inheritdoc />
    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(TransactionOperations.WithoutResult(operation), isolationLevel, cancellationToken);

    /// <inheritdoc />
    public Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return ExecuteInTransactionCoreAsync(operation, isolationLevel, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default) =>
        BeginTransactionCoreAsync(isolationLevel, cancellationToken);

    /// <summary>
    /// Creates the guard that applies the tenant rules to one commit. A provider calls it at most once per commit,
    /// and only when the commit stages tenant entities, then runs every staged tenant entity through it before
    /// writing anything.
    /// </summary>
    /// <remarks>
    /// The current tenant is resolved lazily, per tenant key type: from the innermost
    /// <see cref="ITenantScope{TTenantKey}.Override"/>, otherwise from <see cref="GetCurrentTenantId"/>. Inside a
    /// host scope the guard skips the tenant rules.
    /// </remarks>
    /// <returns>The guard.</returns>
    protected TenantCommitGuard CreateTenantGuard() =>
        new(AmbientTenant.IsHostScope, keyType =>
            AmbientTenant.ResolveCurrentTenant(keyType, () => GetCurrentTenantId(keyType), GetTenantSourceName(keyType)));

    /// <summary>
    /// Gets the current tenant for <paramref name="tenantKeyType"/>, when no tenant override is open. By default it
    /// asks the <see cref="ICurrentTenantProvider{TTenantKey}"/> registered in the service provider passed to the
    /// constructor. Providers override it to use their own source, for example the EF Core context, together with
    /// <see cref="GetTenantSourceName"/>.
    /// </summary>
    /// <remarks>
    /// Return the source's answer as it is: the caller applies the overrides and normalizes it.
    /// </remarks>
    /// <param name="tenantKeyType">The tenant key type of the entities being committed.</param>
    /// <returns>
    /// The current tenant, boxed, or <see langword="null"/> (or a default value) when there is none.
    /// </returns>
    protected virtual object? GetCurrentTenantId(Type tenantKeyType)
    {
        ArgumentNullException.ThrowIfNull(tenantKeyType);
        return ServiceProviderTenantSource.GetCurrentTenantId(_serviceProvider, tenantKeyType);
    }

    /// <summary>
    /// Names the source <see cref="GetCurrentTenantId"/> reads, for the error raised when it returns an empty string.
    /// Override it together with <see cref="GetCurrentTenantId"/>.
    /// </summary>
    /// <remarks>
    /// It is called whether or not a tenant override applies, so it must not read the tenant itself: asking the
    /// provider here would run it inside overrides, where it may have nothing to answer.
    /// </remarks>
    /// <param name="tenantKeyType">The tenant key type of the entities being committed.</param>
    /// <returns>The name of the tenant source.</returns>
    protected virtual string GetTenantSourceName(Type tenantKeyType) =>
        ServiceProviderTenantSource.SourceName(tenantKeyType);

    /// <summary>
    /// Gets a generic repository for <typeparamref name="TEntity"/> that shares this unit of work's changes.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TKey">The type of the entity's identifier.</typeparam>
    /// <returns>The repository.</returns>
    protected IRepository<TEntity, TKey> GetRepository<TEntity, TKey>()
        where TEntity : BaseEntity<TKey>
        where TKey : notnull
        => CreateRepository<TEntity, TKey>();

    /// <summary>
    /// Resolves a custom repository from the service provider passed to the constructor.
    /// </summary>
    /// <typeparam name="TRepo">The repository type, usually a custom repository interface.</typeparam>
    /// <returns>The repository.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no service provider was passed to the constructor, or <typeparamref name="TRepo"/> is not registered.
    /// </exception>
    protected TRepo ResolveRepository<TRepo>() where TRepo : class
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException(
                $"ResolveRepository<{typeof(TRepo).Name}>() requires an IServiceProvider. " +
                "Pass IServiceProvider to the unit of work constructor.");
        }

        return (TRepo?)_serviceProvider.GetService(typeof(TRepo))
            ?? throw new InvalidOperationException(
                $"No service of type '{typeof(TRepo).Name}' is registered.");
    }

    /// <summary>
    /// Creates the provider's generic repository for <typeparamref name="TEntity"/>, bound to this unit of work.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TKey">The type of the entity's identifier.</typeparam>
    /// <returns>The repository.</returns>
    protected abstract IRepository<TEntity, TKey> CreateRepository<TEntity, TKey>()
        where TEntity : BaseEntity<TKey>
        where TKey : notnull;

    /// <summary>
    /// Applies the commit rules to every staged change and persists them.
    /// </summary>
    /// <remarks>
    /// <para>The commit sequence every provider shares, in this order:</para>
    /// <list type="number">
    /// <item><see cref="EnforceTenantRules"/>: the tenant rules, before anything is stamped or written.</item>
    /// <item><see cref="GetStagedChanges"/>: the changes to commit.</item>
    /// <item>Audit fields, with one timestamp for the whole commit (see <see cref="Auditor"/>).</item>
    /// <item>
    /// Versions: each modified <see cref="Entities.Interfaces.IVersionedEntity"/> is incremented, and
    /// <see cref="StagedChange.ExpectVersion"/> is called for every modified or deleted one.
    /// </item>
    /// <item><see cref="PersistStagedChanges"/>.</item>
    /// <item>
    /// On any failure: the versions are restored, and <see cref="TranslateCommitFailure"/> may replace the
    /// exception, for example with a <see cref="Concurrency.ConcurrencyException"/>.
    /// </item>
    /// </list>
    /// <para>
    /// A provider implements the hooks rather than this method. A unit of work that overrides it, as every one did
    /// before the sequence moved here, takes over the whole commit.
    /// </para>
    /// </remarks>
    /// <param name="userId">
    /// The user making the changes, already resolved from the commit's argument and the current-user provider, or
    /// <see langword="null"/> for the default user.
    /// </param>
    protected virtual void CommitCore(TUserKey? userId)
    {
        var expected = new List<(StagedChange Change, long Version)>();

        try
        {
            EnforceTenantRules();
            Stamp(GetStagedChanges(), userId, expected);
            PersistStagedChanges();
        }
        catch (Exception exception)
        {
            if (OnCommitFailed(exception, expected) is { } replacement)
                throw replacement;

            throw;
        }
    }

    /// <summary>
    /// Asynchronously applies the commit rules to every staged change and persists them, in the order described on
    /// <see cref="CommitCore"/>.
    /// </summary>
    /// <param name="userId">
    /// The user making the changes, already resolved from the commit's argument and the current-user provider, or
    /// <see langword="null"/> for the default user.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected virtual async Task CommitCoreAsync(TUserKey? userId, CancellationToken cancellationToken)
    {
        var expected = new List<(StagedChange Change, long Version)>();

        try
        {
            await EnforceTenantRulesAsync(cancellationToken);
            Stamp(GetStagedChanges(), userId, expected);
            await PersistStagedChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            if (OnCommitFailed(exception, expected) is { } replacement)
                throw replacement;

            throw;
        }
    }

    /// <summary>
    /// Applies the tenant rules to the staged changes before anything is stamped or written: stamps the tenant of
    /// added tenant entities, and refuses writes to, or references to, another tenant's rows. Called first by the
    /// commit sequence (see <see cref="CommitCore"/>); a provider builds it on <see cref="CreateTenantGuard"/>.
    /// </summary>
    /// <remarks>
    /// The rules for updates and deletes need the tenant stored in each row, which only the provider can read. So by
    /// default this refuses any staged tenant entity rather than write one unchecked.
    /// </remarks>
    /// <exception cref="NotSupportedException">A tenant entity is staged and the provider does not override this method.</exception>
    protected virtual void EnforceTenantRules()
    {
        foreach (var change in GetStagedChanges())
        {
            if (TenantTraits.IsTenantEntity(change.Entity.GetType()))
            {
                throw new NotSupportedException(
                    $"'{change.Entity.GetType().Name}' is a tenant entity, but '{GetType().Name}' does not enforce " +
                    $"the tenant rules. The provider must override {nameof(EnforceTenantRules)} and " +
                    $"{nameof(EnforceTenantRulesAsync)} to support multi-tenancy.");
            }
        }
    }

    /// <summary>
    /// Asynchronously applies the tenant rules (see <see cref="EnforceTenantRules"/>). By default it runs
    /// <see cref="EnforceTenantRules"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected virtual Task EnforceTenantRulesAsync(CancellationToken cancellationToken)
    {
        EnforceTenantRules();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the changes the next commit writes: every entity to insert, update or remove.
    /// </summary>
    /// <remarks>Called after <see cref="EnforceTenantRules"/>, so it sees any change that step made.</remarks>
    /// <returns>The staged changes.</returns>
    /// <exception cref="NotSupportedException">Always, unless overridden.</exception>
    protected virtual IReadOnlyList<StagedChange> GetStagedChanges() =>
        throw CommitSequenceNotImplemented(nameof(GetStagedChanges));

    /// <summary>
    /// Writes the staged changes to the store, after the commit sequence stamped them. A version conflict must make
    /// it throw, and nothing of the commit may be persisted when it does.
    /// </summary>
    /// <exception cref="NotSupportedException">Always, unless overridden.</exception>
    protected virtual void PersistStagedChanges() =>
        throw CommitSequenceNotImplemented(nameof(PersistStagedChanges));

    /// <summary>
    /// Asynchronously writes the staged changes to the store (see <see cref="PersistStagedChanges"/>). By default it
    /// runs <see cref="PersistStagedChanges"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected virtual Task PersistStagedChangesAsync(CancellationToken cancellationToken)
    {
        PersistStagedChanges();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the exception to throw in place of one a commit failed with, for example a
    /// <see cref="Concurrency.ConcurrencyException"/> for the provider's own conflict error, or
    /// <see langword="null"/> to rethrow it. By the time it is called, the versions are already restored.
    /// </summary>
    /// <param name="exception">The exception the commit failed with.</param>
    /// <returns>The replacement, or <see langword="null"/>.</returns>
    protected virtual Exception? TranslateCommitFailure(Exception exception) => null;

    /// <summary>
    /// Stamps audit fields on every change with one timestamp, then sets up the version check of every versioned
    /// one, recording each expected version so a failed commit can undo it.
    /// </summary>
    private void Stamp(IReadOnlyList<StagedChange> changes, TUserKey? userId, List<(StagedChange Change, long Version)> expected)
    {
        var utcNow = Auditor.UtcNow;

        foreach (var change in changes)
        {
            if (change.GetAuditKind() is { } kind)
                Auditor.Stamp(change.Entity, kind, userId, utcNow);
        }

        foreach (var change in changes)
        {
            if (change.Entity is not IVersionedEntity versioned)
                continue;

            switch (change.State)
            {
                case StagedChangeState.Modified:
                    // Recorded before the store check is set up, so a failure there still restores the version.
                    expected.Add((change, VersionStamper.Stamp(versioned)));
                    change.ExpectVersion(expected[^1].Version);
                    break;

                case StagedChangeState.Deleted:
                    expected.Add((change, versioned.Version));
                    change.ExpectVersion(versioned.Version);
                    break;
            }
        }
    }

    /// <summary>
    /// Restores every version stamped by the failed commit, so the entities carry the version they were read at and
    /// a retried commit checks against it again, and returns the exception to throw instead, if any.
    /// </summary>
    /// <remarks>
    /// Undone in reverse order: a provider that stages an entity more than once stamps it more than once, and only
    /// the first expected version is the one it was read at.
    /// </remarks>
    private Exception? OnCommitFailed(Exception exception, List<(StagedChange Change, long Version)> expected)
    {
        for (var i = expected.Count - 1; i >= 0; i--)
        {
            var (change, version) = expected[i];
            VersionStamper.Revert((IVersionedEntity)change.Entity, version);
            change.RevertExpectedVersion();
        }

        return TranslateCommitFailure(exception);
    }

    private NotSupportedException CommitSequenceNotImplemented(string hook) => new(
        $"'{GetType().Name}' implements neither {nameof(CommitCore)} nor {hook}. A provider's unit of work " +
        $"overrides {nameof(GetStagedChanges)} and {nameof(PersistStagedChanges)} (and their async variants where " +
        $"it has them), or {nameof(CommitCore)} and {nameof(CommitCoreAsync)} to take over the whole commit.");

    /// <summary>
    /// Opens a transaction, or returns <see cref="JoinedTransaction.Instance"/> when one is already open. Override it
    /// to support transactions; both <c>BeginTransactionAsync</c> and <c>ExecuteInTransactionAsync</c> build on it.
    /// </summary>
    /// <param name="isolationLevel">The isolation level, or <see langword="null"/> for the database's default.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The transaction.</returns>
    /// <exception cref="NotSupportedException">Always, unless overridden.</exception>
    protected virtual Task<IUnitOfWorkTransaction> BeginTransactionCoreAsync(
        IsolationLevel? isolationLevel,
        CancellationToken cancellationToken) =>
        throw TransactionErrors.NotSupported(GetType());

    /// <summary>
    /// Runs <paramref name="operation"/> in a transaction from <see cref="BeginTransactionCoreAsync"/>. Override it
    /// to add what the provider needs around the transaction, such as retries on transient failures.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="operation">The work to run in the transaction.</param>
    /// <param name="isolationLevel">The isolation level, or <see langword="null"/> for the database's default.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The result of <paramref name="operation"/>.</returns>
    protected virtual async Task<TResult> ExecuteInTransactionCoreAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel? isolationLevel,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionCoreAsync(isolationLevel, cancellationToken);
        return await RunInTransactionAsync(transaction, operation, cancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="operation"/> and commits <paramref name="transaction"/> when it completes. When it
    /// throws, the transaction is left uncommitted, so disposing it rolls it back.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="transaction">The open transaction. The caller disposes it.</param>
    /// <param name="operation">The work to run in the transaction.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The result of <paramref name="operation"/>.</returns>
    protected static async Task<TResult> RunInTransactionAsync<TResult>(
        IUnitOfWorkTransaction transaction,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(operation);

        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
