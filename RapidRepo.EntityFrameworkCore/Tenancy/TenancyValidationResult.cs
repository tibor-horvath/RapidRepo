namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// The result of <see cref="TenancyValidator.Validate"/>.
/// </summary>
public sealed class TenancyValidationResult
{
    internal TenancyValidationResult(
        Type contextType,
        Type? tenantKeyType,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings)
    {
        ContextType = contextType;
        TenantKeyType = tenantKeyType;
        Errors = errors;
        Warnings = warnings;
    }

    /// <summary>Gets the validated context type.</summary>
    public Type ContextType { get; }

    /// <summary>Gets the problems that make the configuration unsafe. Each names the entity and the fix.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Gets the problems reported without failing, such as foreign keys that don't include <c>TenantId</c>.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Gets a value indicating whether there are no errors.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the tenant key type shared by the model's tenant entities, or <see langword="null"/> when there are none
    /// or they disagree.
    /// </summary>
    internal Type? TenantKeyType { get; }

    /// <summary>
    /// Throws when there are errors.
    /// </summary>
    /// <exception cref="RapidRepoConfigurationException">The validation failed; <see cref="RapidRepoConfigurationException.Problems"/> lists every error.</exception>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
            throw new RapidRepoConfigurationException($"The multi-tenancy configuration of {ContextType.Name} is invalid", Errors);
    }
}
