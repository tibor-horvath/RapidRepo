using RapidRepo.Testing.Conformance;

namespace RapidRepo.EntityFrameworkCore.Tests.Conformance;

// The provider-neutral conformance suite, run against EF Core on two databases.

public sealed class InMemoryCrudConformanceTests : CrudConformanceTests<EfInMemoryConformanceStore>;
public sealed class InMemoryStagedChangesConformanceTests : StagedChangesConformanceTests<EfInMemoryConformanceStore>;
public sealed class InMemorySoftDeleteConformanceTests : SoftDeleteConformanceTests<EfInMemoryConformanceStore>;
public sealed class InMemoryAuditConformanceTests : AuditConformanceTests<EfInMemoryConformanceStore>;
public sealed class InMemoryQueryConformanceTests : QueryConformanceTests<EfInMemoryConformanceStore>;

public sealed class SqliteCrudConformanceTests : CrudConformanceTests<EfSqliteConformanceStore>;
public sealed class SqliteStagedChangesConformanceTests : StagedChangesConformanceTests<EfSqliteConformanceStore>;
public sealed class SqliteSoftDeleteConformanceTests : SoftDeleteConformanceTests<EfSqliteConformanceStore>;
public sealed class SqliteAuditConformanceTests : AuditConformanceTests<EfSqliteConformanceStore>;
public sealed class SqliteQueryConformanceTests : QueryConformanceTests<EfSqliteConformanceStore>;
