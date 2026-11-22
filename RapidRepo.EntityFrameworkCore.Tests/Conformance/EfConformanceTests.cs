using RapidRepo.Testing.Conformance;

namespace RapidRepo.EntityFrameworkCore.Tests.Conformance;

// The provider-neutral conformance suite, run against EF Core on two databases.

public sealed class InMemoryCrudConformanceTests : CrudConformanceTests<EfInMemoryConformanceStore>;
public sealed class InMemoryStagedChangesConformanceTests : StagedChangesConformanceTests<EfInMemoryConformanceStore>;
public sealed class InMemorySoftDeleteConformanceTests : SoftDeleteConformanceTests<EfInMemoryConformanceStore>;
public sealed class InMemoryAuditConformanceTests : AuditConformanceTests<EfInMemoryConformanceStore>;
public sealed class InMemoryQueryConformanceTests : QueryConformanceTests<EfInMemoryConformanceStore>;
public sealed class InMemoryQueryExpressionConformanceTests : QueryExpressionConformanceTests<EfInMemoryConformanceStore>;
public sealed class InMemoryConcurrencyConformanceTests : ConcurrencyConformanceTests<EfInMemoryConformanceStore>;

// No InMemory ConcurrencyAtomicityConformanceTests: EF Core InMemory has no transactions, so rows written before the
// conflict stay written. SQLite covers atomicity. No InMemory TransactionConformanceTests either, for the same reason.

public sealed class SqliteCrudConformanceTests : CrudConformanceTests<EfSqliteConformanceStore>;
public sealed class SqliteStagedChangesConformanceTests : StagedChangesConformanceTests<EfSqliteConformanceStore>;
public sealed class SqliteSoftDeleteConformanceTests : SoftDeleteConformanceTests<EfSqliteConformanceStore>;
public sealed class SqliteAuditConformanceTests : AuditConformanceTests<EfSqliteConformanceStore>;
public sealed class SqliteQueryConformanceTests : QueryConformanceTests<EfSqliteConformanceStore>;
public sealed class SqliteQueryExpressionConformanceTests : QueryExpressionConformanceTests<EfSqliteConformanceStore>;
public sealed class SqliteConcurrencyConformanceTests : ConcurrencyConformanceTests<EfSqliteConformanceStore>;
public sealed class SqliteConcurrencyAtomicityConformanceTests : ConcurrencyAtomicityConformanceTests<EfSqliteConformanceStore>;
public sealed class SqliteTransactionConformanceTests : TransactionConformanceTests<EfSqliteConformanceStore>;

// Multi-tenancy, opt-in through ITenantConformanceStore.

public sealed class InMemoryTenantConformanceTests : TenantConformanceTests<EfInMemoryTenantConformanceStore>;
public sealed class SqliteTenantConformanceTests : TenantConformanceTests<EfSqliteTenantConformanceStore>;
