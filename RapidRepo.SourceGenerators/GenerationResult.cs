namespace RapidRepo.SourceGenerators;

/// <summary>Everything one <c>[GenerateUnitOfWork]</c> class produces: its files and its diagnostics.</summary>
/// <remarks>
/// The generator pipeline carries only value-equal types like this one between steps, never symbols or a
/// <c>Compilation</c>. When an edit leaves the result equal to the previous one, Roslyn skips the output step and
/// keeps the generated files as they were, so the IDE does not re-bind the code that depends on them on every
/// keystroke.
/// </remarks>
internal sealed record GenerationResult(
    EquatableArray<GeneratedFile> Files,
    EquatableArray<DiagnosticInfo> Diagnostics)
{
    public static GenerationResult Empty { get; } =
        new(EquatableArray<GeneratedFile>.Empty, EquatableArray<DiagnosticInfo>.Empty);
}
