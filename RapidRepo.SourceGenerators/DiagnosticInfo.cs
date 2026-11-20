using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace RapidRepo.SourceGenerators;

/// <summary>
/// A diagnostic held as data. A <see cref="Diagnostic"/> keeps its <see cref="Location"/>, which ties it to one
/// syntax tree, and does not compare by value.
/// </summary>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params string[] messageArgs) =>
        new(descriptor, LocationInfo.From(location), new EquatableArray<string>(messageArgs.ToImmutableArray()));

    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(
            Descriptor,
            Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None,
            MessageArgs.Cast<object>().ToArray());
}
