using AwesomeAssertions;
using RapidRepo.Repositories;
using System.Linq.Expressions;
using Xunit;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// The minimum set of condition expressions every provider must translate: comparisons, <c>&amp;&amp;</c>, <c>||</c> and
/// <c>!</c>, captured values, null checks, <see cref="string"/> <c>StartsWith</c>/<c>EndsWith</c>/<c>Contains</c>, and
/// <c>Contains</c> on an in-memory collection. A provider may support more, and should throw
/// <see cref="NotSupportedException"/> for anything it cannot translate rather than filter on the client.
/// </summary>
/// <typeparam name="TStore">The provider's store.</typeparam>
public abstract class QueryExpressionConformanceTests<TStore> : ConformanceTests<TStore>
    where TStore : IConformanceStore, new()
{
    private static readonly ReadOptions IncludeDeleted = new() { IncludeDeleted = true };

    private void SeedNamed() => SeedWidgets(
        new Widget { Name = "Alpha", Rank = 1 },
        new Widget { Name = "Bravo", Rank = 2 },
        new Widget { Name = "Charlie", Rank = 3 },
        new Widget { Name = "100%_off", Rank = 4 });

    [Fact]
    public void Comparisons_CoverEveryOperator()
    {
        SeedNamed();

        Names(w => w.Rank == 2).Should().BeEquivalentTo(["Bravo"]);
        Names(w => w.Rank != 2).Should().BeEquivalentTo(["Alpha", "Charlie", "100%_off"]);
        Names(w => w.Rank < 2).Should().BeEquivalentTo(["Alpha"]);
        Names(w => w.Rank <= 2).Should().BeEquivalentTo(["Alpha", "Bravo"]);
        Names(w => w.Rank > 3).Should().BeEquivalentTo(["100%_off"]);
        Names(w => w.Rank >= 3).Should().BeEquivalentTo(["Charlie", "100%_off"]);
        Names(w => 2 == w.Rank).Should().BeEquivalentTo(["Bravo"], "the column may be on either side");
    }

    [Fact]
    public void LogicalOperators_CombineConditions()
    {
        SeedNamed();

        Names(w => w.Rank == 1 || w.Rank == 3).Should().BeEquivalentTo(["Alpha", "Charlie"]);
        Names(w => w.Rank >= 2 && w.Rank <= 3).Should().BeEquivalentTo(["Bravo", "Charlie"]);
        Names(w => !(w.Rank < 3)).Should().BeEquivalentTo(["Charlie", "100%_off"]);
        Names(w => (w.Rank == 1 || w.Rank == 4) && w.Name != "Alpha").Should().BeEquivalentTo(["100%_off"]);
    }

    [Fact]
    public void CapturedValues_AreEvaluatedBeforeTranslation()
    {
        SeedNamed();
        var name = "Bravo";
        var threshold = new { Min = 2 };

        Names(w => w.Name == name).Should().BeEquivalentTo(["Bravo"]);
        Names(w => w.Rank > threshold.Min).Should().BeEquivalentTo(["Charlie", "100%_off"]);
        Names(w => w.Rank == Increment(threshold.Min)).Should().BeEquivalentTo(["Charlie"], "a method on a captured value runs once, on the client");
    }

    [Fact]
    public void StringMethods_MatchLiterally()
    {
        SeedNamed();

        Names(w => w.Name.StartsWith("Al")).Should().BeEquivalentTo(["Alpha"]);
        Names(w => w.Name.EndsWith("lie")).Should().BeEquivalentTo(["Charlie"]);
        Names(w => w.Name.Contains("ra")).Should().BeEquivalentTo(["Bravo"]);
        Names(w => w.Name.Contains("%_")).Should().BeEquivalentTo(["100%_off"], "LIKE wildcards in the argument match literally");
        Names(w => w.Name.StartsWith("_")).Should().BeEmpty("an unescaped _ would match every name");
    }

    [Fact]
    public void CollectionContains_MatchesAnyListedValue()
    {
        SeedNamed();
        int[] ranks = [1, 3];
        var names = new List<string> { "Bravo" };

        Names(w => ranks.Contains(w.Rank)).Should().BeEquivalentTo(["Alpha", "Charlie"], "C# 14 binds this to MemoryExtensions.Contains over a span");
        Names(w => names.Contains(w.Name)).Should().BeEquivalentTo(["Bravo"]);
        Names(w => Enumerable.Contains(ranks, w.Rank)).Should().BeEquivalentTo(["Alpha", "Charlie"]);
        Names(w => Array.Empty<int>().Contains(w.Rank)).Should().BeEmpty();
    }

    [Fact]
    public void NullChecks_OnNullableColumns()
    {
        var id = SeedNotes(new Note { Title = "Live" }, new Note { Title = "Dead" })[1].Id;
        Commit(s => s.Notes().DeleteById(id));
        DateTime? none = null;

        Titles(n => n.DeletedAt != null).Should().BeEquivalentTo(["Dead"]);
        Titles(n => n.DeletedAt == null).Should().BeEquivalentTo(["Live"]);
        Titles(n => n.DeletedAt == none).Should().BeEquivalentTo(["Live"], "a captured null compares as IS NULL");
        Titles(n => n.DeletedBy == null).Should().BeEquivalentTo(["Live"], "nullable Guid columns too");
        Titles(n => n.DeletedBy != null && n.ModifiedBy != null).Should().BeEquivalentTo(["Dead"]);
    }

    [Fact]
    public void DateTimeColumns_CompareChronologically()
    {
        SeedNotes(new Note { Title = "First" });
        Clock.UtcNow = T2;
        SeedNotes(new Note { Title = "Second" });
        var between = T1.AddDays(1);

        Titles(n => n.CreatedAt < between).Should().BeEquivalentTo(["First"]);
        Titles(n => n.CreatedAt >= T2).Should().BeEquivalentTo(["Second"]);
        InNewSession(s => s.Notes().Count(n => n.CreatedAt <= T2)).Should().Be(2);
    }

    private static int Increment(int value) => value + 1;

    private List<string> Names(Expression<Func<Widget, bool>> condition) =>
        InNewSession(s => s.Widgets().GetAll(condition).Select(w => w.Name).ToList());

    private List<string> Titles(Expression<Func<Note, bool>> condition) =>
        InNewSession(s => s.Notes().GetAll(condition, options: IncludeDeleted).Select(n => n.Title).ToList());
}
