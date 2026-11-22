namespace RapidRepo;

/// <summary>
/// The exception thrown when RapidRepo is configured in a way that is not safe to run, for example a tenant entity
/// without the tenant filter. It lists every problem found, not only the first, each naming what is wrong and how to
/// fix it.
/// </summary>
/// <remarks>
/// <para>
/// It is thrown by validations that run before any data is touched: on first use of a model, at host startup, or
/// when a validation is called explicitly. Fix the configuration; retrying does not help.
/// </para>
/// <para>
/// It derives from <see cref="InvalidOperationException"/>, so existing handlers keep catching it. Read
/// <see cref="Problems"/> rather than parsing <see cref="Exception.Message"/>, for example in a test or a health check.
/// </para>
/// </remarks>
public sealed class RapidRepoConfigurationException : InvalidOperationException
{
    private const string DefaultSummary = "The RapidRepo configuration is invalid";

    /// <summary>
    /// Initializes a new instance of the <see cref="RapidRepoConfigurationException"/> class.
    /// </summary>
    public RapidRepoConfigurationException()
        : this(DefaultSummary, [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RapidRepoConfigurationException"/> class with a message and no
    /// listed problems.
    /// </summary>
    /// <param name="message">The message.</param>
    public RapidRepoConfigurationException(string message)
        : this(message, innerException: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RapidRepoConfigurationException"/> class with a message, no
    /// listed problems, and the exception that caused it.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The exception that caused this one, if any.</param>
    public RapidRepoConfigurationException(string message, Exception? innerException)
        : base(message, innerException)
    {
        Problems = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RapidRepoConfigurationException"/> class for a list of problems,
    /// with a message that starts with <paramref name="summary"/> and lists them one per line.
    /// </summary>
    /// <param name="summary">What was found invalid, for example "The multi-tenancy configuration of AppDbContext is invalid".</param>
    /// <param name="problems">The problems, each naming what is wrong and how to fix it.</param>
    public RapidRepoConfigurationException(string summary, IEnumerable<string> problems)
        : this(Snapshot(problems), summary)
    {
    }

    private RapidRepoConfigurationException(IReadOnlyList<string> problems, string summary)
        : base(FormatMessage(summary, problems))
    {
        Problems = problems;
    }

    /// <summary>
    /// Gets the problems found, each naming what is wrong and how to fix it. Empty when the exception was created
    /// with a plain message.
    /// </summary>
    public IReadOnlyList<string> Problems { get; }

    private static IReadOnlyList<string> Snapshot(IEnumerable<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        return problems.ToArray();
    }

    private static string FormatMessage(string summary, IReadOnlyList<string> problems)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return problems.Count == 0
            ? $"{summary}."
            : $"{summary} ({problems.Count} problem{(problems.Count == 1 ? "" : "s")}):{Environment.NewLine}" +
              string.Join(Environment.NewLine, problems.Select(p => $"- {p}"));
    }
}
