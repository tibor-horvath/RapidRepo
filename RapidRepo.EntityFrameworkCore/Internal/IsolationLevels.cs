using System.Data;

namespace RapidRepo.EntityFrameworkCore.Internal;

/// <summary>
/// Compares isolation levels, to decide whether a transaction that is already open gives the guarantees a nested
/// call asks for.
/// </summary>
internal static class IsolationLevels
{
    /// <summary>
    /// Whether a transaction at <paramref name="open"/> gives at least the guarantees of <paramref name="requested"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="IsolationLevel.ReadUncommitted"/>, <see cref="IsolationLevel.ReadCommitted"/>,
    /// <see cref="IsolationLevel.RepeatableRead"/> and <see cref="IsolationLevel.Serializable"/> are ordered from
    /// weakest to strictest. <see cref="IsolationLevel.Snapshot"/> and <see cref="IsolationLevel.Chaos"/> are not
    /// ordered against them, so they only satisfy a request for themselves.
    /// </remarks>
    public static bool Satisfies(IsolationLevel open, IsolationLevel requested) =>
        open == requested || (Strictness(open) is { } openRank && Strictness(requested) is { } requestedRank && openRank >= requestedRank);

    private static int? Strictness(IsolationLevel level) => level switch
    {
        IsolationLevel.ReadUncommitted => 0,
        IsolationLevel.ReadCommitted => 1,
        IsolationLevel.RepeatableRead => 2,
        IsolationLevel.Serializable => 3,
        _ => null,
    };
}
