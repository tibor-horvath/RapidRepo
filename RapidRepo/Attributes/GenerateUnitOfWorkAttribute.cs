namespace RapidRepo.Attributes;

/// <summary>
/// Instructs <c>RapidRepo.SourceGenerators</c> to generate a repository-properties interface
/// and matching partial-class implementations for the attributed Unit of Work class.
/// </summary>
/// <remarks>
/// The attributed class must be <c>partial</c>.
/// DbSet-backed properties use <c>GetRepository&lt;TEntity, TKey&gt;()</c> (zero DI overhead).
/// Custom repository interfaces are resolved via <c>ResolveRepository&lt;TRepo&gt;()</c> (DI-backed).
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class GenerateUnitOfWorkAttribute(params Type[] dbContextTypes) : Attribute
{
    /// <summary>The DbContext types whose DbSet properties are used to generate repository properties.</summary>
    public Type[] DbContextTypes { get; } = dbContextTypes;

    /// <summary>Override the generated interface name. Default: <c>I{ClassName}Repositories</c>.</summary>
    public string? RepositoriesInterfaceName { get; set; }

    /// <summary>
    /// Namespace prefixes to include when scanning for custom repository interfaces (empty = all).
    /// Mirrors <see cref="RapidRepo.Extensions.DependencyInjection.RapidRepoOptions"/> include filters.
    /// </summary>
    public string[] IncludeNamespaces { get; set; } = [];

    /// <summary>Namespace prefixes to exclude when scanning for custom repository interfaces.</summary>
    public string[] ExcludeNamespaces { get; set; } = [];
}
