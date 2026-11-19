using FluentAssertions;
using RapidRepo.Auditing;
using RapidRepo.Entities;
using RapidRepo.Repositories;

namespace RapidRepo.Tests.Repositories;

/// <summary>
/// The provider-neutral write rules of <see cref="WriteRepositoryBase{TEntity, TKey}"/>, tested through a fake
/// provider that records what it is asked to stage. The default range hooks are left alone, so they are tested too.
/// </summary>
public class WriteRepositoryBaseTests
{
    private static readonly DateTime Now = new(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc);

    private sealed class Plain : BaseEntity<int>;

    private sealed class Doc : BaseAuditableDeletableEntity<int, Guid>;

    [Fact]
    public void Guards_RejectNullArguments()
    {
        var repo = new FakeRepository<Doc>();

        var calls = new Action[]
        {
            () => repo.Add(null!),
            () => repo.AddRange(null!),
            () => repo.AddAsync(null!).GetAwaiter().GetResult(),
            () => repo.AddRangeAsync(null!).GetAwaiter().GetResult(),
            () => repo.Update(null!),
            () => repo.UpdateRange(null!),
            () => repo.Delete(null!),
            () => repo.DeleteRange(null!),
            () => repo.Restore(null!),
            () => repo.RestoreRange(null!),
            () => repo.HardDelete(null!),
            () => repo.HardDeleteRange(null!),
        };

        foreach (var call in calls)
            call.Should().Throw<ArgumentNullException>();

        repo.Log.Should().BeEmpty();
    }

    [Fact]
    public async Task DefaultRangeHooks_StageEachEntityInTurn()
    {
        var repo = new FakeRepository<Plain>();
        Plain[] entities = [new() { Id = 1 }, new() { Id = 2 }];

        repo.AddRange(entities);
        await repo.AddRangeAsync(entities);
        repo.UpdateRange(entities);
        repo.HardDeleteRange(entities);

        repo.Log.Should().Equal(
            "Insert 1", "Insert 2",
            "InsertAsync 1", "InsertAsync 2",
            "Update 1", "Update 2",
            "Remove 1", "Remove 2");
    }

    [Fact]
    public void RangeOperations_EnumerateTheirSourceOnce()
    {
        var repo = new FakeRepository<Doc>();
        var source = new CountingSequence<Doc>(new Doc { Id = 1 }, new Doc { Id = 2 });

        repo.DeleteRange(source);

        source.Enumerations.Should().Be(1, "the source may be a live query over the set being changed");
        repo.Log.Should().Equal("SoftDeleted 1", "SoftDeleted 2");
    }

    [Fact]
    public void Delete_OfAnEntityWithoutSoftDelete_RemovesIt()
    {
        var repo = new FakeRepository<Plain>();

        repo.Delete(new Plain { Id = 1 });
        repo.DeleteRange([new Plain { Id = 2 }]);

        repo.Log.Should().Equal("Remove 1", "Remove 2");
    }

    [Fact]
    public void Delete_OfADeletableEntity_MarksItWithTheRepositorysClock()
    {
        var repo = new FakeRepository<Doc>();
        var doc = new Doc { Id = 1 };

        repo.Delete(doc);

        repo.Log.Should().Equal("SoftDeleted 1");
        doc.DeletedAt.Should().Be(Now);
    }

    [Fact]
    public void Delete_OfAnAlreadyDeletedEntity_KeepsItsMarks_ButIsStillStaged()
    {
        var repo = new FakeRepository<Doc>();
        var deletedAt = Now.AddDays(-2);
        var deletedBy = Guid.NewGuid();
        var doc = new Doc { Id = 1, DeletedAt = deletedAt, DeletedBy = deletedBy };

        repo.Delete(doc);

        repo.Log.Should().Equal("SoftDeleted 1");
        doc.DeletedAt.Should().Be(deletedAt);
        doc.DeletedBy.Should().Be(deletedBy);
    }

    [Fact]
    public void Restore_ClearsDeletedAtAndDeletedBy()
    {
        var repo = new FakeRepository<Doc>();
        var doc = new Doc { Id = 1, DeletedAt = Now, DeletedBy = Guid.NewGuid() };

        repo.Restore(doc);

        repo.Log.Should().Equal("Restored 1");
        doc.DeletedAt.Should().BeNull();
        doc.DeletedBy.Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(FakeRepository<Plain>.Restore))]
    [InlineData(nameof(FakeRepository<Plain>.RestoreRange))]
    [InlineData(nameof(FakeRepository<Plain>.RestoreById))]
    [InlineData(nameof(FakeRepository<Plain>.RestoreByIdAsync))]
    public void RestoreOperations_OfAnEntityWithoutSoftDelete_ThrowAndNameTheOperation(string operation)
    {
        var repo = new FakeRepository<Plain>();
        Action act = operation switch
        {
            nameof(repo.Restore) => () => repo.Restore(new Plain()),
            nameof(repo.RestoreRange) => () => repo.RestoreRange([new Plain()]),
            nameof(repo.RestoreById) => () => repo.RestoreById(1),
            _ => () => repo.RestoreByIdAsync(1).GetAwaiter().GetResult(),
        };

        act.Should().Throw<InvalidOperationException>().WithMessage($"'{operation}' requires 'Plain'*");
        repo.Log.Should().BeEmpty();
    }

    [Fact]
    public async Task ByIdOperations_LookUpWithTheDocumentedIncludeDeletedDefaults()
    {
        var repo = new FakeRepository<Doc>(new Doc { Id = 1 });

        repo.DeleteById(1);
        await repo.DeleteByIdAsync(1);
        repo.RestoreById(1);
        await repo.RestoreByIdAsync(1);
        repo.HardDeleteById(1);
        await repo.HardDeleteByIdAsync(1);

        repo.Lookups.Should().Equal(
            (1, false), (1, false),
            (1, true), (1, true),
            (1, false), (1, false));
    }

    [Fact]
    public async Task ByIdOperations_ForAMissingKey_StageNothing()
    {
        var repo = new FakeRepository<Doc>();

        repo.DeleteById(42);
        await repo.DeleteByIdAsync(42);
        repo.RestoreById(42);
        await repo.RestoreByIdAsync(42);
        repo.HardDeleteById(42);
        await repo.HardDeleteByIdAsync(42);

        repo.Lookups.Should().HaveCount(6);
        repo.Log.Should().BeEmpty();
    }

    [Fact]
    public void ByIdOperations_StageTheEntityFound()
    {
        var repo = new FakeRepository<Doc>(new Doc { Id = 1 }, new Doc { Id = 2, DeletedAt = Now }, new Doc { Id = 3 });

        repo.DeleteById(1);
        repo.RestoreById(2);
        repo.HardDeleteById(3);

        repo.Log.Should().Equal("SoftDeleted 1", "Restored 2", "Remove 3");
    }

    /// <summary>
    /// Records what it is asked to stage. Like a change-tracking provider, it calls <c>applyChange</c> as part of
    /// staging a deletion change.
    /// </summary>
    private sealed class FakeRepository<TEntity>(params TEntity[] stored) : WriteRepositoryBase<TEntity, int>
        where TEntity : BaseEntity<int>
    {
        public List<string> Log { get; } = [];

        public List<(int Id, bool IncludeDeleted)> Lookups { get; } = [];

        protected override TimeProvider TimeProvider { get; } = new FixedClock(Now);

        protected override void StageInsert(TEntity entity) => Log.Add($"Insert {entity.Id}");

        protected override ValueTask StageInsertAsync(TEntity entity, CancellationToken cancellationToken)
        {
            Log.Add($"InsertAsync {entity.Id}");
            return ValueTask.CompletedTask;
        }

        protected override void StageUpdate(TEntity entity) => Log.Add($"Update {entity.Id}");

        protected override void StageRemove(TEntity entity) => Log.Add($"Remove {entity.Id}");

        protected override void StageDeletionChange(TEntity entity, EntityChangeKind kind, Action applyChange)
        {
            applyChange();
            Log.Add($"{kind} {entity.Id}");
        }

        protected override TEntity? FindForWrite(int id, bool includeDeleted)
        {
            Lookups.Add((id, includeDeleted));
            return stored.FirstOrDefault(e => e.Id == id);
        }

        protected override ValueTask<TEntity?> FindForWriteAsync(int id, bool includeDeleted, CancellationToken cancellationToken) =>
            ValueTask.FromResult(FindForWrite(id, includeDeleted));
    }

    private sealed class CountingSequence<T>(params T[] items) : IEnumerable<T>
    {
        public int Enumerations { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            Enumerations++;
            return ((IEnumerable<T>)items).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
