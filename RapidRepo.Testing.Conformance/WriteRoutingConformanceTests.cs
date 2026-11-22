using AwesomeAssertions;
using RapidRepo.Repositories;
using RapidRepo.Repositories.Interfaces;
using Xunit;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// The write operations that the write rules call from other operations go through the repository's virtual members,
/// so a custom repository's overrides see those calls too.
/// </summary>
/// <remarks>
/// <c>DeleteRange</c> and <c>RestoreRange</c> of soft-deletable entities change each entity themselves rather than
/// calling <c>Delete</c> or <c>Restore</c>; a repository that needs to see them overrides the range members.
/// </remarks>
/// <typeparam name="TStore">The provider's store.</typeparam>
public abstract class WriteRoutingConformanceTests<TStore> : ConformanceTests<TStore>
    where TStore : IWriteRoutingConformanceStore, new()
{
    private const string Delete = "Delete";
    private const string HardDelete = "HardDelete";
    private const string HardDeleteRange = "HardDeleteRange";
    private const string Restore = "Restore";

    private static readonly ReadOptions IncludeDeleted = new() { IncludeDeleted = true };

    private readonly List<string> _calls = [];

    [Fact]
    public void Delete_OfAnEntityWithoutSoftDelete_GoesThroughTheHardDeleteOverride()
    {
        var id = SeedWidgets(new Widget())[0].Id;
        var (session, widgets) = Recording<Widget>();

        widgets.Delete(widgets.GetById(id)!);
        session.UnitOfWork.Commit();

        _calls.Should().Equal(Delete, HardDelete);
        InNewSession(s => s.Widgets().ExistsById(id)).Should().BeFalse();
    }

    [Fact]
    public void DeleteById_OfAnEntityWithoutSoftDelete_GoesThroughTheDeleteAndHardDeleteOverrides()
    {
        var id = SeedWidgets(new Widget())[0].Id;
        var (session, widgets) = Recording<Widget>();

        widgets.DeleteById(id);
        session.UnitOfWork.Commit();

        _calls.Should().Equal(Delete, HardDelete);
        InNewSession(s => s.Widgets().ExistsById(id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteByIdAsync_OfAnEntityWithoutSoftDelete_GoesThroughTheDeleteAndHardDeleteOverrides()
    {
        var id = SeedWidgets(new Widget())[0].Id;
        var (session, widgets) = Recording<Widget>();

        await widgets.DeleteByIdAsync(id);
        await session.UnitOfWork.CommitAsync();

        _calls.Should().Equal(Delete, HardDelete);
        InNewSession(s => s.Widgets().ExistsById(id)).Should().BeFalse();
    }

    [Fact]
    public void DeleteById_OfADeletableEntity_GoesThroughTheDeleteOverride_Only()
    {
        var id = SeedNotes(new Note())[0].Id;
        var (session, notes) = Recording<Note>();

        notes.DeleteById(id);
        session.UnitOfWork.Commit();

        _calls.Should().Equal(Delete);
        InNewSession(s => s.Notes().ExistsById(id, IncludeDeleted)).Should().BeTrue();
    }

    [Fact]
    public void DeleteRange_OfEntitiesWithoutSoftDelete_GoesThroughTheHardDeleteRangeOverride()
    {
        SeedWidgets(new Widget(), new Widget());
        var (session, widgets) = Recording<Widget>();

        widgets.DeleteRange(widgets.GetAll());
        session.UnitOfWork.Commit();

        _calls.Should().Equal(HardDeleteRange);
        InNewSession(s => s.Widgets().Count()).Should().Be(0);
    }

    [Fact]
    public void HardDeleteById_GoesThroughTheHardDeleteOverride()
    {
        var id = SeedNotes(new Note())[0].Id;
        var (session, notes) = Recording<Note>();

        notes.HardDeleteById(id);
        session.UnitOfWork.Commit();

        _calls.Should().Equal(HardDelete);
        InNewSession(s => s.Notes().ExistsById(id, IncludeDeleted)).Should().BeFalse();
    }

    [Fact]
    public async Task HardDeleteByIdAsync_GoesThroughTheHardDeleteOverride()
    {
        var id = SeedWidgets(new Widget())[0].Id;
        var (session, widgets) = Recording<Widget>();

        await widgets.HardDeleteByIdAsync(id);
        await session.UnitOfWork.CommitAsync();

        _calls.Should().Equal(HardDelete);
        InNewSession(s => s.Widgets().ExistsById(id)).Should().BeFalse();
    }

    [Fact]
    public void RestoreById_GoesThroughTheRestoreOverride()
    {
        var id = SeedNotes(new Note())[0].Id;
        Commit(s => s.Notes().DeleteById(id));
        var (session, notes) = Recording<Note>();

        notes.RestoreById(id);
        session.UnitOfWork.Commit();

        _calls.Should().Equal(Restore);
        InNewSession(s => s.Notes().ExistsById(id)).Should().BeTrue();
    }

    [Fact]
    public async Task RestoreByIdAsync_GoesThroughTheRestoreOverride()
    {
        var id = SeedNotes(new Note())[0].Id;
        Commit(s => s.Notes().DeleteById(id));
        var (session, notes) = Recording<Note>();

        await notes.RestoreByIdAsync(id);
        await session.UnitOfWork.CommitAsync();

        _calls.Should().Equal(Restore);
        InNewSession(s => s.Notes().ExistsById(id)).Should().BeTrue();
    }

    private (IConformanceSession Session, IRepository<TEntity, int> Repository) Recording<TEntity>()
        where TEntity : Entities.BaseEntity<int>
    {
        var session = OpenSession();
        return (session, Store.CreateRecordingRepository<TEntity, int>(session, _calls));
    }
}
