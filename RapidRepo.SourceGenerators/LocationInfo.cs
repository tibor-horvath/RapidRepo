using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace RapidRepo.SourceGenerators;

/// <summary>A source location held as data, so that it compares by value and pins no syntax tree.</summary>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    /// <summary>Captures <paramref name="location"/>, or returns <see langword="null"/> for one outside source.</summary>
    public static LocationInfo? From(Location? location) =>
        location is { IsInSource: true }
            ? new LocationInfo(location.SourceTree!.FilePath, location.SourceSpan, location.GetLineSpan().Span)
            : null;

    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);
}
