using RapidRepo.Auditing;
using RapidRepo.Repositories.Interfaces;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// The base of every conformance test class: a fresh <typeparamref name="TStore"/> per test, a controllable clock,
/// and helpers for working through sessions.
/// </summary>
/// <remarks>
/// Assertions about what was persisted read through a new session (<see cref="InNewSession{T}"/>), never the session
/// that wrote, so a provider's in-memory state (such as EF Core's change tracker) cannot mask what reached the store.
/// </remarks>
/// <typeparam name="TStore">The provider's store.</typeparam>
public abstract class ConformanceTests<TStore> : IDisposable
    where TStore : IConformanceStore, new()
{
    /// <summary>The user stamped when a commit names none.</summary>
    protected static readonly Guid DefaultUser = ConformanceDefaults.DefaultUser;

    /// <summary>A user that commits explicitly.</summary>
    protected static readonly Guid Alice = new("00000000-0000-0000-0000-00000000a11c");

    /// <summary>Another user that commits explicitly.</summary>
    protected static readonly Guid Bob = new("00000000-0000-0000-0000-000000000b0b");

    /// <summary>The time every test starts at.</summary>
    protected static readonly DateTime T1 = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>A later time, after advancing <see cref="Clock"/>.</summary>
    protected static readonly DateTime T2 = new(2026, 3, 8, 17, 30, 0, DateTimeKind.Utc);

    private readonly List<IConformanceSession> _sessions = [];

    /// <summary>Gets the store for this test.</summary>
    protected TStore Store { get; } = new();

    /// <summary>Gets the clock every session stamps audit fields with.</summary>
    protected ConformanceClock Clock { get; } = new(T1);

    /// <summary>Opens a session, disposed with the test.</summary>
    /// <param name="currentUserProvider">The current-user provider of the session's unit of work, if any.</param>
    protected IConformanceSession OpenSession(ICurrentUserProvider<Guid>? currentUserProvider = null) =>
        Track(Store.OpenSession(Clock, currentUserProvider));

    /// <summary>Disposes <paramref name="session"/> with the test.</summary>
    /// <param name="session">A session opened by the derived class.</param>
    /// <returns>The same session.</returns>
    protected IConformanceSession Track(IConformanceSession session)
    {
        _sessions.Add(session);
        return session;
    }

    /// <summary>Runs <paramref name="work"/> in a new session and commits it as <paramref name="user"/>.</summary>
    protected void Commit(Action<IConformanceSession> work, Guid? user = null)
    {
        var session = OpenSession();
        work(session);
        session.UnitOfWork.Commit(user);
    }

    /// <summary>Reads through a new session that writes nothing.</summary>
    protected T InNewSession<T>(Func<IConformanceSession, T> read) => read(OpenSession());

    /// <summary>Adds and commits <paramref name="notes"/>, returning them with their generated keys.</summary>
    protected Note[] SeedNotes(params Note[] notes)
    {
        Commit(s => s.Notes().AddRange(notes));
        return notes;
    }

    /// <summary>Adds and commits <paramref name="tickets"/>, returning them with their generated keys.</summary>
    protected Ticket[] SeedTickets(params Ticket[] tickets)
    {
        Commit(s => s.Tickets().AddRange(tickets));
        return tickets;
    }

    /// <summary>Adds and commits <paramref name="widgets"/>, returning them with their generated keys.</summary>
    protected Widget[] SeedWidgets(params Widget[] widgets)
    {
        Commit(s => s.Widgets().AddRange(widgets));
        return widgets;
    }

    public void Dispose()
    {
        foreach (var session in _sessions)
            session.Dispose();

        Store.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Values a provider's store has to use.</summary>
public static class ConformanceDefaults
{
    /// <summary>The default user of every unit of work a store opens: the user stamped when a commit names none.</summary>
    public static readonly Guid DefaultUser = new("00000000-0000-0000-0000-00000000d0d0");
}

/// <summary>Shorthands for the conformance repositories.</summary>
public static class ConformanceSessionExtensions
{
    public static IRepository<Note, int> Notes(this IConformanceSession session) => session.Repository<Note, int>();

    public static IRepository<Widget, int> Widgets(this IConformanceSession session) => session.Repository<Widget, int>();

    public static IRepository<Ticket, int> Tickets(this IConformanceSession session) => session.Repository<Ticket, int>();

    public static IRepository<Invoice, int> Invoices(this IConformanceSession session) => session.Repository<Invoice, int>();

    public static IRepository<Payment, int> Payments(this IConformanceSession session) => session.Repository<Payment, int>();
}

/// <summary>A current-user provider that returns a fixed user and counts how often it is asked.</summary>
/// <param name="userId">The user to return, or <see langword="null"/> for none.</param>
public sealed class ConformanceCurrentUser(Guid? userId) : ICurrentUserProvider<Guid>
{
    /// <summary>Gets how many times the unit of work asked for the current user.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public Guid? GetCurrentUserId()
    {
        Calls++;
        return userId;
    }
}

/// <summary>A clock the tests move by hand.</summary>
/// <param name="utcNow">The initial time.</param>
public sealed class ConformanceClock(DateTime utcNow) : TimeProvider
{
    /// <summary>Gets or sets the current UTC time.</summary>
    public DateTime UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(UtcNow, DateTimeKind.Utc));
}
