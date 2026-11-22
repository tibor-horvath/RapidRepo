using AwesomeAssertions;
using RapidRepo.Auditing;
using RapidRepo.Entities;

namespace RapidRepo.Tests.Auditing;

public class EntityChangeClassifierTests
{
    private static readonly DateTime Earlier = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class Doc : BaseAuditableDeletableEntity<int, Guid>;

    private sealed class Plain : BaseEntity<int>;

    [Theory]
    [InlineData(false, false, EntityChangeKind.Modified)]
    [InlineData(false, true, EntityChangeKind.SoftDeleted)]
    [InlineData(true, false, EntityChangeKind.Restored)]
    [InlineData(true, true, EntityChangeKind.Modified)]
    public void ClassifyUpdate_FollowsTheDeletionTransition(bool wasDeleted, bool isDeleted, EntityChangeKind expected)
    {
        var doc = new Doc { DeletedAt = isDeleted ? Later : null };

        EntityChangeClassifier.ClassifyUpdate(doc, wasDeleted ? Earlier : null).Should().Be(expected);
    }

    [Fact]
    public void ClassifyUpdate_OfANonDeletableEntity_IsModified_WhateverTheOriginalValue()
    {
        EntityChangeClassifier.ClassifyUpdate(new Plain(), originalDeletedAt: null).Should().Be(EntityChangeKind.Modified);
        EntityChangeClassifier.ClassifyUpdate(new Plain(), Earlier).Should().Be(EntityChangeKind.Modified);
    }

    [Fact]
    public void ClassifyUpdate_RejectsANullEntity()
    {
        var act = () => EntityChangeClassifier.ClassifyUpdate(null!, originalDeletedAt: null);

        act.Should().Throw<ArgumentNullException>().WithParameterName("entity");
    }
}
