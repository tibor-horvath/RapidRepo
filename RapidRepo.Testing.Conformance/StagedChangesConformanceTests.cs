using AwesomeAssertions;
using Xunit;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// Writes are staged: nothing reaches the store until the unit of work commits.
/// </summary>
/// <typeparam name="TStore">The provider's store.</typeparam>
public abstract class StagedChangesConformanceTests<TStore> : ConformanceTests<TStore>
    where TStore : IConformanceStore, new()
{
    [Fact]
    public void Add_WithoutCommit_IsNotPersisted()
    {
        var session = OpenSession();

        session.Widgets().Add(new Widget { Name = "Uncommitted" });

        InNewSession(s => s.Widgets().Count()).Should().Be(0);
    }

    [Fact]
    public void Update_WithoutCommit_IsNotPersisted()
    {
        var id = SeedWidgets(new Widget { Name = "Original" })[0].Id;
        var session = OpenSession();
        var widget = session.Widgets().GetById(id)!;

        widget.Name = "Uncommitted";
        session.Widgets().Update(widget);

        InNewSession(s => s.Widgets().GetById(id))!.Name.Should().Be("Original");
    }

    [Fact]
    public void Delete_WithoutCommit_IsNotPersisted()
    {
        var widgetId = SeedWidgets(new Widget())[0].Id;
        var noteId = SeedNotes(new Note())[0].Id;
        var session = OpenSession();

        session.Widgets().DeleteById(widgetId);
        session.Notes().DeleteById(noteId);

        InNewSession(s => s.Widgets().ExistsById(widgetId)).Should().BeTrue();
        InNewSession(s => s.Notes().ExistsById(noteId)).Should().BeTrue();
    }

    [Fact]
    public void Commit_PersistsEveryStagedChange_Together()
    {
        var keepId = SeedWidgets(new Widget { Name = "Keep" })[0].Id;
        var dropId = SeedWidgets(new Widget { Name = "Drop" })[0].Id;
        var session = OpenSession();

        session.Widgets().Add(new Widget { Name = "Added" });
        var keep = session.Widgets().GetById(keepId)!;
        keep.Name = "Kept";
        session.Widgets().Update(keep);
        session.Widgets().DeleteById(dropId);
        session.UnitOfWork.Commit();

        InNewSession(s => s.Widgets().GetAll(w => w.Name)).Should().BeEquivalentTo(["Kept", "Added"]);
    }

    [Fact]
    public void AddThenDelete_OfANonDeletableEntity_InOneCommit_PersistsNothing()
    {
        var session = OpenSession();
        var widget = new Widget { Name = "Short-lived" };

        session.Widgets().Add(widget);
        session.Widgets().Delete(widget);
        session.UnitOfWork.Commit();

        InNewSession(s => s.Widgets().Count()).Should().Be(0);
    }

    [Fact]
    public void DeleteById_OfTheDefaultKey_LeavesPendingInsertsAlone()
    {
        var session = OpenSession();

        session.Widgets().Add(new Widget { Name = "Pending" });
        session.Notes().Add(new Note { Title = "Pending" });
        session.Widgets().DeleteById(default);
        session.Widgets().HardDeleteById(default);
        session.Notes().DeleteById(default);
        session.UnitOfWork.Commit();

        InNewSession(s => s.Widgets().Count()).Should().Be(1);
        InNewSession(s => s.Notes().Count()).Should().Be(1);
    }

    [Fact]
    public void Sessions_DoNotSeeEachOthersStagedChanges()
    {
        var writer = OpenSession();
        var reader = OpenSession();

        writer.Widgets().Add(new Widget { Name = "Staged" });

        reader.Widgets().Count().Should().Be(0);
    }
}
