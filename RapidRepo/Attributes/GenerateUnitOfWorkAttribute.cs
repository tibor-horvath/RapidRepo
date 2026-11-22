namespace RapidRepo.Attributes;

/// <summary>
/// Instructs <c>RapidRepo.SourceGenerators</c> to generate a repository-properties interface
/// and matching partial-class implementations for the attributed Unit of Work class.
/// </summary>
/// <remarks>
/// <para>The attributed class must be <c>partial</c>.</para>
/// <para>
/// Each source is either a type with <c>DbSet&lt;TEntity&gt;</c> properties, such as an EF Core <c>DbContext</c>, or
/// an entity type deriving from <see cref="Entities.BaseEntity{TId}"/>. Entities from either kind of source get
/// <c>IRepository&lt;TEntity, TKey&gt;</c> properties backed by <c>GetRepository&lt;TEntity, TKey&gt;()</c> (zero DI
/// overhead). An entity source's property is named after the entity in plural, for example <c>Product</c> becomes
/// <c>Products</c>.
/// </para>
/// <para>Custom repository interfaces are resolved via <c>ResolveRepository&lt;TRepo&gt;()</c> (DI-backed).</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class GenerateUnitOfWorkAttribute(params Type[] sources) : Attribute
{
    /// <summary>
    /// The DbContext types and entity types whose entities get generic repository properties.
    /// </summary>
    public Type[] Sources { get; } = sources;

    /// <summary>Override the generated interface name. Default: <c>I{ClassName}Repositories</c>.</summary>
    public string? RepositoriesInterfaceName { get; set; }

    /// <summary>
    /// Namespace prefixes to include when scanning for custom repository interfaces (empty = all).
    /// Mirrors the <c>RapidRepoOptions</c> include filters in RapidRepo.Extensions.DependencyInjection.
    /// </summary>
    public string[] IncludeNamespaces { get; set; } = [];

    /// <summary>Namespace prefixes to exclude when scanning for custom repository interfaces.</summary>
    public string[] ExcludeNamespaces { get; set; } = [];
}
