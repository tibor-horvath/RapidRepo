using FluentAssertions;
using RapidRepo.Auditing;
using RapidRepo.Entities;

namespace RapidRepo.Tests.Auditing;

public class AuditStamperTests
{
    private static readonly DateTime Now = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly Guid DefaultUser = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    private readonly AuditStamper<Guid> _sut = new(DefaultUser, new FixedTimeProvider(Now));

    private sealed class Doc : BaseAuditableDeletableEntity<int, Guid>;

    private sealed class Plain : BaseEntity<int>;

    private sealed class IntUserDoc : BaseAuditableDeletableEntity<int, int>;

    [Fact]
    public void Added_StampsCreatedFields()
    {
        var doc = new Doc();

        _sut.Stamp(doc, EntityChangeKind.Added, User);

        doc.CreatedAt.Should().Be(Now);
        doc.CreatedBy.Should().Be(User);
        doc.ModifiedAt.Should().BeNull();
        doc.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void Added_OfAnEntityAlreadyMarkedDeleted_AlsoStampsTheDeletion()
    {
        var doc = new Doc { DeletedAt = Now.AddHours(-1) };

        _sut.Stamp(doc, EntityChangeKind.Added, User);

        doc.CreatedAt.Should().Be(Now);
        doc.CreatedBy.Should().Be(User);
        doc.DeletedAt.Should().Be(Now);
        doc.DeletedBy.Should().Be(User);
        doc.ModifiedAt.Should().BeNull("an insert is not a modification");
    }

    [Fact]
    public void Restored_OfAnAuditableEntityWithoutSoftDelete_StampsOnlyTheModification()
    {
        var entity = new AuditedOnly();

        _sut.Stamp(entity, EntityChangeKind.Restored, User);

        entity.ModifiedAt.Should().Be(Now);
        entity.ModifiedBy.Should().Be(User);
    }

    [Fact]
    public void Stamp_RejectsANullEntity()
    {
        var act = () => _sut.Stamp(null!, EntityChangeKind.Added);

        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class AuditedOnly : BaseAuditableEntity<int, Guid>;

    [Fact]
    public void Modified_StampsModifiedFields_AndLeavesDeletionMarksAlone()
    {
        var deletedAt = Now.AddDays(-3);
        var deletedBy = Guid.NewGuid();
        var doc = new Doc { DeletedAt = deletedAt, DeletedBy = deletedBy };

        _sut.Stamp(doc, EntityChangeKind.Modified, User);

        doc.ModifiedAt.Should().Be(Now);
        doc.ModifiedBy.Should().Be(User);
        doc.DeletedAt.Should().Be(deletedAt);
        doc.DeletedBy.Should().Be(deletedBy);
    }

    [Fact]
    public void SoftDeleted_StampsDeletedAndModifiedFields()
    {
        var doc = new Doc { DeletedAt = Now.AddHours(-1) };

        _sut.Stamp(doc, EntityChangeKind.SoftDeleted, User);

        doc.DeletedAt.Should().Be(Now);
        doc.DeletedBy.Should().Be(User);
        doc.ModifiedAt.Should().Be(Now);
        doc.ModifiedBy.Should().Be(User);
    }

    [Fact]
    public void Restored_ClearsDeletionMarks_AndStampsModifiedFields()
    {
        var doc = new Doc { DeletedAt = Now.AddDays(-1), DeletedBy = Guid.NewGuid() };

        _sut.Stamp(doc, EntityChangeKind.Restored, User);

        doc.DeletedAt.Should().BeNull();
        doc.DeletedBy.Should().BeNull();
        doc.ModifiedAt.Should().Be(Now);
        doc.ModifiedBy.Should().Be(User);
    }

    [Fact]
    public void NoUser_FallsBackToDefaultUser()
    {
        var doc = new Doc();

        _sut.Stamp(doc, EntityChangeKind.Added);

        doc.CreatedBy.Should().Be(DefaultUser);
    }

    [Fact]
    public void DifferentUserKeyType_StampsTimesOnly()
    {
        var doc = new IntUserDoc();

        _sut.Stamp(doc, EntityChangeKind.Added, User);

        doc.CreatedAt.Should().Be(Now);
        doc.CreatedBy.Should().Be(0);
    }

    [Fact]
    public void NonAuditableEntity_IsLeftUntouched()
    {
        var act = () => _sut.Stamp(new Plain(), EntityChangeKind.SoftDeleted, User);

        act.Should().NotThrow();
    }

    [Fact]
    public void ExplicitTime_IsUsedInsteadOfClock()
    {
        var commitTime = Now.AddMinutes(7);
        var doc = new Doc();

        _sut.Stamp(doc, EntityChangeKind.Added, User, commitTime);

        doc.CreatedAt.Should().Be(commitTime);
    }

    [Fact]
    public void UtcNow_ComesFromTimeProvider()
    {
        _sut.UtcNow.Should().Be(Now);
        _sut.UtcNow.Kind.Should().Be(DateTimeKind.Utc);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
