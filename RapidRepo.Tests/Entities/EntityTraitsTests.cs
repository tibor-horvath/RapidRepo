using FluentAssertions;
using RapidRepo.Entities;

namespace RapidRepo.Tests.Entities;

public class EntityTraitsTests
{
    private sealed class GuidUserDoc : BaseAuditableDeletableEntity<int, Guid>;

    private sealed class LongUserDoc : BaseAuditableDeletableEntity<int, long>;

    private sealed class AnonymousDoc : BaseAuditableDeletableEntity<int>;

    private sealed class Plain : BaseEntity<int>;

    [Fact]
    public void SupportsSoftDelete_ReflectsIDeletableEntity()
    {
        EntityTraits<GuidUserDoc>.SupportsSoftDelete.Should().BeTrue();
        EntityTraits<AnonymousDoc>.SupportsSoftDelete.Should().BeTrue();
        EntityTraits<Plain>.SupportsSoftDelete.Should().BeFalse();
    }

    [Fact]
    public void TracksDeletedBy_OnlyForEntitiesWithAUserKey()
    {
        EntityTraits<GuidUserDoc>.TracksDeletedBy.Should().BeTrue();
        EntityTraits<LongUserDoc>.TracksDeletedBy.Should().BeTrue();
        EntityTraits<AnonymousDoc>.TracksDeletedBy.Should().BeFalse();
        EntityTraits<Plain>.TracksDeletedBy.Should().BeFalse();
    }

    [Fact]
    public void ClearDeletedBy_ClearsWhateverTheUserKeyType()
    {
        var guidDoc = new GuidUserDoc { DeletedBy = Guid.NewGuid() };
        var longDoc = new LongUserDoc { DeletedBy = 42 };

        EntityTraits<GuidUserDoc>.ClearDeletedBy(guidDoc);
        EntityTraits<LongUserDoc>.ClearDeletedBy(longDoc);

        guidDoc.DeletedBy.Should().BeNull();
        longDoc.DeletedBy.Should().BeNull();
    }

    [Fact]
    public void ClearDeletedBy_LeavesDeletedAtAlone()
    {
        var deletedAt = DateTime.UtcNow;
        var doc = new GuidUserDoc { DeletedAt = deletedAt, DeletedBy = Guid.NewGuid() };

        EntityTraits<GuidUserDoc>.ClearDeletedBy(doc);

        doc.DeletedAt.Should().Be(deletedAt);
    }

    [Fact]
    public void ClearDeletedBy_OnEntityWithoutDeletedBy_DoesNothing()
    {
        var act = () => EntityTraits<Plain>.ClearDeletedBy(new Plain());

        act.Should().NotThrow();
    }
}
