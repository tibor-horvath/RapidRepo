using AwesomeAssertions;
using RapidRepo.Auditing;
using RapidRepo.Concurrency;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Tests.UnitOfWork;

/// <summary>
/// The commit sequence of <see cref="UnitOfWorkBase{TUserKey}"/>, run against a provider that implements only the
/// hooks: tenant rules, then the staged changes, audit, versions and persisting, and the rollback of versions on
/// failure.
/// </summary>
public class CommitSequenceTests
{
    private static readonly Guid DefaultUser = new("00000000-0000-0000-0000-0000000000d0");
    private static readonly Guid Alice = new("00000000-0000-0000-0000-00000000a11c");
    private static readonly DateTime Now = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Commit_RunsTheHooksInOrder()
    {
        var uow = new HookUnitOfWork();

        uow.Commit();

        uow.Calls.Should().Equal("EnforceTenantRules", "GetStagedChanges", "PersistStagedChanges");
    }

    [Fact]
    public async Task CommitAsync_RunsTheAsyncHooksInOrder()
    {
        var uow = new HookUnitOfWork();

        await uow.CommitAsync();

        uow.Calls.Should().Equal("EnforceTenantRulesAsync", "GetStagedChanges", "PersistStagedChangesAsync");
    }

    [Fact]
    public void Commit_StampsEveryChange_WithOneTimestamp_AndTheCommitsUser()
    {
        var added = new Audited();
        var modified = new Audited();
        var uow = new HookUnitOfWork(
            new FakeChange(added, StagedChangeState.Added),
            new FakeChange(modified, StagedChangeState.Modified));

        uow.Commit(Alice);

        added.CreatedAt.Should().Be(Now);
        added.CreatedBy.Should().Be(Alice);
        modified.ModifiedAt.Should().Be(Now);
        modified.ModifiedBy.Should().Be(Alice);
    }

    [Fact]
    public void Commit_StampsASoftDelete_FromTheOriginalDeletedAt()
    {
        var note = new Audited { DeletedAt = Now.AddDays(-1) };
        var uow = new HookUnitOfWork(new FakeChange(note, StagedChangeState.Modified) { OriginalDeletedAt = (true, null) });

        uow.Commit(Alice);

        note.DeletedAt.Should().Be(Now);
        note.DeletedBy.Should().Be(Alice);
    }

    [Fact]
    public void Commit_WithoutAKnownOriginalDeletedAt_StampsAnOrdinaryEdit()
    {
        var deletedAt = Now.AddDays(-1);
        var note = new Audited { DeletedAt = deletedAt };
        var uow = new HookUnitOfWork(new FakeChange(note, StagedChangeState.Modified));

        uow.Commit(Alice);

        note.DeletedAt.Should().Be(deletedAt);
        note.ModifiedBy.Should().Be(Alice);
    }

    [Fact]
    public void Commit_DoesNotStampAHardDelete()
    {
        var removed = new Audited();
        var uow = new HookUnitOfWork(new FakeChange(removed, StagedChangeState.Deleted));

        uow.Commit(Alice);

        removed.ModifiedAt.Should().BeNull();
        removed.CreatedAt.Should().NotBe(Now);
    }

    [Fact]
    public void Commit_IncrementsAnUpdatedVersion_AndExpectsTheOldOne()
    {
        var updated = new Versioned { Version = 3 };
        var change = new FakeChange(updated, StagedChangeState.Modified);
        var uow = new HookUnitOfWork(change);

        uow.Commit();

        updated.Version.Should().Be(4);
        change.Expected.Should().Equal(3);
    }

    [Fact]
    public void Commit_ExpectsADeletedVersion_WithoutChangingIt()
    {
        var deleted = new Versioned { Version = 3 };
        var change = new FakeChange(deleted, StagedChangeState.Deleted);
        var uow = new HookUnitOfWork(change);

        uow.Commit();

        deleted.Version.Should().Be(3);
        change.Expected.Should().Equal(3);
    }

    [Fact]
    public void Commit_LeavesAnInsertedVersion_Unchecked()
    {
        var inserted = new Versioned { Version = 0 };
        var change = new FakeChange(inserted, StagedChangeState.Added);
        var uow = new HookUnitOfWork(change);

        uow.Commit();

        inserted.Version.Should().Be(0);
        change.Expected.Should().BeEmpty();
    }

    [Fact]
    public async Task FailedCommit_RestoresTheVersions_AndThrowsTheTranslation()
    {
        var updated = new Versioned { Version = 3 };
        var change = new FakeChange(updated, StagedChangeState.Modified);
        var conflict = new ConcurrencyException([updated]);
        var uow = new HookUnitOfWork(change)
        {
            PersistFailure = new InvalidOperationException("conflict in the store"),
            Translate = _ => conflict,
        };

        var commit = () => uow.CommitAsync();

        (await commit.Should().ThrowAsync<ConcurrencyException>()).Which.Should().BeSameAs(conflict);
        updated.Version.Should().Be(3);
        change.Reverted.Should().BeTrue();
    }

    [Fact]
    public void FailedCommit_RestoresTheReadVersion_OfAnEntityStagedTwice()
    {
        var updated = new Versioned { Version = 3 };
        var uow = new HookUnitOfWork(
            new FakeChange(updated, StagedChangeState.Modified),
            new FakeChange(updated, StagedChangeState.Modified))
        {
            PersistFailure = new InvalidOperationException("store down"),
        };

        var commit = () => uow.Commit();

        commit.Should().Throw<InvalidOperationException>();
        updated.Version.Should().Be(3);
    }

    [Fact]
    public void FailedCommit_RethrowsTheOriginal_WhenThereIsNoTranslation()
    {
        var updated = new Versioned { Version = 3 };
        var uow = new HookUnitOfWork(new FakeChange(updated, StagedChangeState.Modified))
        {
            PersistFailure = new InvalidOperationException("store down"),
        };

        var commit = () => uow.Commit();

        commit.Should().Throw<InvalidOperationException>().WithMessage("store down");
        updated.Version.Should().Be(3);
    }

    [Fact]
    public void Commit_RefusesAVersionedChange_WhenTheProviderDoesNotCheckVersions()
    {
        var updated = new Versioned { Version = 3 };
        var uow = new HookUnitOfWork(new UncheckedChange(updated, StagedChangeState.Modified));

        var commit = () => uow.Commit();

        commit.Should().Throw<NotSupportedException>().WithMessage("*does not check versions*");
        updated.Version.Should().Be(3, "the increment is undone");
        uow.Calls.Should().NotContain("PersistStagedChanges");
    }

    [Fact]
    public void DefaultTenantRules_RefuseATenantEntity_BeforeAnythingIsWritten()
    {
        var uow = new HookUnitOfWork(new FakeChange(new TenantOwned(), StagedChangeState.Added)) { UseDefaultTenantRules = true };

        var commit = () => uow.Commit();

        commit.Should().Throw<NotSupportedException>().WithMessage("*tenant entity*");
        uow.Calls.Should().NotContain("PersistStagedChanges");
    }

    [Fact]
    public void DefaultTenantRules_AllowChangesWithoutTenantEntities()
    {
        var uow = new HookUnitOfWork(new FakeChange(new Audited(), StagedChangeState.Added)) { UseDefaultTenantRules = true };

        uow.Commit();

        uow.Calls.Should().Contain("PersistStagedChanges");
    }

    [Fact]
    public async Task DefaultAsyncHooks_FallBackToTheSyncOnes()
    {
        var uow = new SyncOnlyUnitOfWork();

        await uow.CommitAsync();

        uow.Persisted.Should().BeTrue();
    }

    [Fact]
    public void Commit_WithoutTheHooks_ExplainsWhatToOverride()
    {
        var uow = new NoHooksUnitOfWork();

        var commit = () => uow.Commit();

        commit.Should().Throw<NotSupportedException>().WithMessage("*GetStagedChanges*PersistStagedChanges*CommitCore*");
    }

    public sealed class Audited : BaseAuditableDeletableEntity<int, Guid>;

    public sealed class Versioned : BaseVersionedEntity<int>;

    public sealed class TenantOwned : BaseEntity<int>, ITenantEntity<Guid>
    {
        public Guid TenantId { get; set; }
    }

    private class FakeChange(object entity, StagedChangeState state) : StagedChange(entity, state)
    {
        public (bool Known, DateTime? Value) OriginalDeletedAt { get; init; }

        public List<long> Expected { get; } = [];

        public bool Reverted { get; private set; }

        protected internal override bool TryGetOriginalDeletedAt(out DateTime? originalDeletedAt)
        {
            originalDeletedAt = OriginalDeletedAt.Value;
            return OriginalDeletedAt.Known;
        }

        protected internal override void ExpectVersion(long expectedVersion) => Expected.Add(expectedVersion);

        protected internal override void RevertExpectedVersion() => Reverted = true;
    }

    /// <summary>A provider's change that does not override <see cref="StagedChange.ExpectVersion"/>.</summary>
    private sealed class UncheckedChange(object entity, StagedChangeState state) : StagedChange(entity, state);

    private sealed class FixedClock() : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private abstract class TestUnitOfWork() : UnitOfWorkBase<Guid>(DefaultUser, serviceProvider: null, new FixedClock())
    {
        protected override IRepository<TEntity, TKey> CreateRepository<TEntity, TKey>() =>
            throw new NotSupportedException();
    }

    private sealed class HookUnitOfWork(params StagedChange[] changes) : TestUnitOfWork
    {
        public List<string> Calls { get; } = [];

        public bool UseDefaultTenantRules { get; init; }

        public Exception? PersistFailure { get; init; }

        public Func<Exception, Exception?> Translate { get; init; } = _ => null;

        protected override void EnforceTenantRules()
        {
            if (UseDefaultTenantRules)
            {
                base.EnforceTenantRules();
                return;
            }

            Calls.Add(nameof(EnforceTenantRules));
        }

        protected override Task EnforceTenantRulesAsync(CancellationToken cancellationToken)
        {
            Calls.Add(nameof(EnforceTenantRulesAsync));
            return Task.CompletedTask;
        }

        protected override IReadOnlyList<StagedChange> GetStagedChanges()
        {
            Calls.Add(nameof(GetStagedChanges));
            return changes;
        }

        protected override void PersistStagedChanges()
        {
            Calls.Add(nameof(PersistStagedChanges));

            if (PersistFailure is not null)
                throw PersistFailure;
        }

        protected override Task PersistStagedChangesAsync(CancellationToken cancellationToken)
        {
            Calls.Add(nameof(PersistStagedChangesAsync));

            return PersistFailure is null ? Task.CompletedTask : Task.FromException(PersistFailure);
        }

        protected override Exception? TranslateCommitFailure(Exception exception) => Translate(exception);
    }

    private sealed class SyncOnlyUnitOfWork : TestUnitOfWork
    {
        public bool Persisted { get; private set; }

        protected override IReadOnlyList<StagedChange> GetStagedChanges() => [];

        protected override void PersistStagedChanges() => Persisted = true;
    }

    private sealed class NoHooksUnitOfWork : TestUnitOfWork;
}
