namespace RapidRepo.EntityFrameworkCore.Internal;

/// <summary>
/// A key value, single or composite, compared by value.
/// </summary>
internal sealed class KeyValues(object?[] values) : IEquatable<KeyValues>
{
    public object?[] Values { get; } = values;

    public bool Equals(KeyValues? other) =>
        other is not null && Values.Length == other.Values.Length && Values.SequenceEqual(other.Values);

    public override bool Equals(object? obj) => Equals(obj as KeyValues);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in Values)
            hash.Add(value);
        return hash.ToHashCode();
    }
}
