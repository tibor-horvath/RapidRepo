using RapidRepo.Auditing;
using RapidRepo.Entities;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.Tenancy;
using System.Data;

namespace RapidRepo.UnitOfWork;

/// <summary>
/// The provider-neutral base of a unit of work: commit entry points, audit stamping, and the repository accessors
/// that <c>[GenerateUnitOfWork]</c> emits code against.
/// </summary>
/// <remarks>
/// Providers derive from this class, for example <c>EfUnitOfWork&lt;TUserKey&gt;</c> in
/// RapidRepo.EntityFrameworkCore. A provider maps its staged changes to <see cref="EntityChangeKind"/> values and
/// stamps each entity through <see cref="Auditor"/> before persisting.
/// </remarks>
/// <typeparam name="TUserKey">The type of the user key recorded in audit fields.</typeparam>
public abstract class UnitOfWorkBase<TUserKey> : IUnitOfWork<TUserKey>
    where TUserKey : struct
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly Lazy<ICurrentUserProvider<TUserKey>?> _currentUserProvider;
    private readonly Lazy<bool> _requireCurrentUser;

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

        // Resolved on first commit, not here: the unit of work may be created before the request has a user.
        _currentUserProvider = new Lazy<ICurrentUserProvider<TUserKey>?>(() =>
            currentUserProvider
            ?? (ICurrentUserProvider<TUserKey>?)serviceProvider?.GetService(typeof(ICurrentUserProvider<TUserKey>)));

        _requireCurrentUser = new Lazy<bool>(() =>
            (serviceProvider?.GetService(typeof(CurrentUserOptions)) as CurrentUserOptions)?.RequireCurrentUser ?? false);

        // Strict mode is checked where a user field is stamped, so a commit that stamps none never needs a user.
        Auditor.OnMissingUser = () => RequireCurrentUser ? MissingUserException() : null;
    }

    /// <summary>
    /// Gets the audit stamper the provider applies to each changed entity at commit time.
    /// </summary>
    protected AuditStamper<TUserKey> Auditor { get; }

    /// <summary>
    /// Gets the provider asked for the audit user when a commit names none, or <see langword="null"/> when there
    /// is none. It is the one passed to the constructor, otherwise the one registered in the service provider.
    /// </summary>
    protected ICurrentUserProvider<TUserKey>? CurrentUserProvider => _currentUserProvider.Value;

    /// <summary>
    /// Gets a value indicating whether a commit without a user throws instead of stamping the default user key into
    /// a user field (<c>CreatedBy</c>, <c>ModifiedBy</c>, <c>DeletedBy</c>). A commit that stamps no user field,
    /// for example one that changes only entities without user audit fields, does not need a user. By default it
    /// comes from <see cref="CurrentUserOptions"/> in the service provider, and is <see langword="false"/> without one.
    /// </summary>
    protected virtual bool RequireCurrentUser => _requireCurrentUser.Value;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// <see cref="RequireCurrentUser"/> is set, a user field is to be stamped, and neither <paramref name="userId"/>
    /// nor the current-user provider supplies a user. Nothing is persisted.
    /// </exception>
    public void Commit(TUserKey? userId = null) => CommitCore(ResolveUserId(userId));

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// <see cref="RequireCurrentUser"/> is set, a user field is to be stamped, and neither <paramref name="userId"/>
    /// nor the current-user provider supplies a user. Nothing is persisted.
    /// </exception>
    public Task CommitAsync(TUserKey? userId = null, CancellationToken cancellationToken = default) =>
        CommitCoreAsync(ResolveUserId(userId), cancellationToken);

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
    /// Resolves the commit's audit user: the explicit <paramref name="userId"/>, else the current-user provider,
    /// else <see langword="null"/>, which <see cref="Auditor"/> stamps as the default user key (or rejects in
    /// strict mode).
    /// </summary>
    private TUserKey? ResolveUserId(TUserKey? userId) => userId ?? CurrentUserProvider?.GetCurrentUserId();

    private InvalidOperationException MissingUserException() => new(
        $"The commit has no audit user to stamp: no userId was passed to Commit/CommitAsync, and " +
        (CurrentUserProvider is null
            ? $"no ICurrentUserProvider<{typeof(TUserKey).Name}> is configured. "
            : $"the ICurrentUserProvider<{typeof(TUserKey).Name}> returned null. ") +
        "RequireCurrentUser is set, so the default user key is not used. Pass a userId, configure a " +
        "current-user provider, or turn RequireCurrentUser off for commits that run without a user.");

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

        if (_serviceProvider?.GetService(typeof(ICurrentTenantProvider<>).MakeGenericType(tenantKeyType)) is not { } provider)
            return null;

        return CurrentTenantProviders.GetCurrentTenantId(provider, tenantKeyType);
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
        $"The {nameof(ICurrentTenantProvider<int>)}<{tenantKeyType.Name}>";

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
    /// Stamps audit fields on every staged change and persists them.
    /// </summary>
    /// <param name="userId">
    /// The user making the changes, already resolved from the commit's argument and the current-user provider, or
    /// <see langword="null"/> for the default user.
    /// </param>
    protected abstract void CommitCore(TUserKey? userId);

    /// <summary>
    /// Asynchronously stamps audit fields on every staged change and persists them.
    /// </summary>
    /// <param name="userId">
    /// The user making the changes, already resolved from the commit's argument and the current-user provider, or
    /// <see langword="null"/> for the default user.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task CommitCoreAsync(TUserKey? userId, CancellationToken cancellationToken);

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
