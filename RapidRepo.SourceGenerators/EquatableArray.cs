using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace RapidRepo.SourceGenerators;

/// <summary>An immutable array that compares by its elements, for use in generator pipeline models.</summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items) => _items = items;

    public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

    public int Length => Items.Length;

    // default(EquatableArray<T>) behaves as empty rather than throwing.
    private ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

    public bool Equals(EquatableArray<T> other)
    {
        var items = Items;
        var otherItems = other.Items;
        if (items.Length != otherItems.Length)
            return false;

        for (var i = 0; i < items.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(items[i], otherItems[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in Items)
            hash = unchecked(hash * 31 + EqualityComparer<T>.Default.GetHashCode(item));
        return hash;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
