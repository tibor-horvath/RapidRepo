using AwesomeAssertions;
using RapidRepo.Concurrency;
using RapidRepo.Entities;

namespace RapidRepo.Tests.Concurrency;

public class VersionStamperTests
{
    private sealed class Versioned : BaseVersionedEntity<int>;

    [Fact]
    public void Stamp_IncrementsTheVersion_AndReturnsTheExpectedOne()
    {
        var entity = new Versioned { Version = 4 };

        var expected = VersionStamper.Stamp(entity);

        expected.Should().Be(4);
        entity.Version.Should().Be(5);
    }

    [Fact]
    public void Stamp_AtTheMaximum_Wraps()
    {
        var entity = new Versioned { Version = long.MaxValue };

        VersionStamper.Stamp(entity);

        entity.Version.Should().Be(long.MinValue);
    }

    [Fact]
    public void Revert_RestoresTheExpectedVersion()
    {
        var entity = new Versioned { Version = 4 };
        var expected = VersionStamper.Stamp(entity);

        VersionStamper.Revert(entity, expected);

        entity.Version.Should().Be(4);
    }

    [Fact]
    public void NullEntity_Throws()
    {
        var stamp = () => VersionStamper.Stamp(null!);
        var revert = () => VersionStamper.Revert(null!, 0);

        stamp.Should().Throw<ArgumentNullException>().WithParameterName("entity");
        revert.Should().Throw<ArgumentNullException>().WithParameterName("entity");
    }
}
