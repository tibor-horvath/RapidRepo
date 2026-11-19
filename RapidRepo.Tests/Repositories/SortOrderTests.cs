using FluentAssertions;
using RapidRepo.Repositories;

namespace RapidRepo.Tests.Repositories;

public class SortOrderTests
{
    private sealed class Person
    {
        public string Name { get; init; } = "";
        public int Age { get; init; }
    }

    [Fact]
    public void By_CreatesSingleAscendingKey()
    {
        var order = SortOrder<Person>.By(p => p.Name);

        order.Keys.Should().ContainSingle();
        order.Keys[0].Descending.Should().BeFalse();
        order.Keys[0].Key.ReturnType.Should().Be<string>();
    }

    [Fact]
    public void ByDescending_CreatesSingleDescendingKey()
    {
        var order = SortOrder<Person>.ByDescending(p => p.Age);

        order.Keys.Should().ContainSingle().Which.Descending.Should().BeTrue();
    }

    [Fact]
    public void ThenBy_AppendsKeysInOrder()
    {
        var order = SortOrder<Person>.By(p => p.Name).ThenByDescending(p => p.Age).ThenBy(p => p.Name.Length);

        order.Keys.Select(k => k.Descending).Should().Equal(false, true, false);
        order.Keys.Select(k => k.Key.ReturnType).Should().Equal(typeof(string), typeof(int), typeof(int));
    }

    [Fact]
    public void ThenBy_ReturnsNewInstance_LeavingOriginalUnchanged()
    {
        var original = SortOrder<Person>.By(p => p.Name);

        var extended = original.ThenBy(p => p.Age);

        extended.Should().NotBeSameAs(original);
        original.Keys.Should().ContainSingle();
        extended.Keys.Should().HaveCount(2);
    }

    [Fact]
    public void Keys_CanBeCompiledAndEvaluated()
    {
        var order = SortOrder<Person>.By(p => p.Age);

        var value = order.Keys[0].Key.Compile().DynamicInvoke(new Person { Age = 42 });

        value.Should().Be(42);
    }

    [Fact]
    public void NullKey_Throws()
    {
        var act = () => SortOrder<Person>.By<string>(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
