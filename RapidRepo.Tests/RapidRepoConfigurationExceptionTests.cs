using AwesomeAssertions;

namespace RapidRepo.Tests;

public class RapidRepoConfigurationExceptionTests
{
    [Fact]
    public void Problems_AreListedInTheMessage_OnePerLine()
    {
        var exception = new RapidRepoConfigurationException("The setup is invalid", ["first", "second"]);

        exception.Problems.Should().Equal("first", "second");
        exception.Message.Should().Be(
            $"The setup is invalid (2 problems):{Environment.NewLine}- first{Environment.NewLine}- second");
    }

    [Fact]
    public void OneProblem_IsSingular()
    {
        var exception = new RapidRepoConfigurationException("The setup is invalid", ["only"]);

        exception.Message.Should().StartWith("The setup is invalid (1 problem):");
    }

    [Fact]
    public void Problems_AreSnapshotted_SoTheCallersListCanChangeAfterwards()
    {
        var problems = new List<string> { "first" };

        var exception = new RapidRepoConfigurationException("The setup is invalid", problems);
        problems.Add("added later");

        exception.Problems.Should().Equal("first");
    }

    [Fact]
    public void APlainMessage_HasNoProblems()
    {
        var inner = new InvalidOperationException("cause");

        var exception = new RapidRepoConfigurationException("Something is off.", inner);

        exception.Problems.Should().BeEmpty();
        exception.Message.Should().Be("Something is off.");
        exception.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void IsAnInvalidOperationException_SoExistingHandlersKeepCatchingIt()
    {
        Exception exception = new RapidRepoConfigurationException();

        exception.Should().BeAssignableTo<InvalidOperationException>();
        exception.Message.Should().Be("The RapidRepo configuration is invalid.");
    }
}
