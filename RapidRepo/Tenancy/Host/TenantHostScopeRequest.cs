namespace RapidRepo.Tenancy.Host;

/// <summary>
/// Where a host scope is being opened.
/// </summary>
/// <param name="CallerMemberName">The calling member.</param>
/// <param name="CallerFilePath">The calling source file.</param>
/// <param name="CallerLineNumber">The calling line.</param>
public sealed record TenantHostScopeRequest(string CallerMemberName, string CallerFilePath, int CallerLineNumber)
{
    /// <inheritdoc />
    public override string ToString() => $"{CallerMemberName} ({CallerFilePath}:{CallerLineNumber})";
}
