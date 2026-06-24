namespace RapidRepo.Attributes;

/// <summary>
/// Overrides the generated property name for a custom repository interface
/// when used with <see cref="GenerateUnitOfWorkAttribute"/>.
/// </summary>
/// <remarks>
/// Apply to a custom repository interface to control the name of the generated
/// UoW property. Useful for irregular plurals (e.g. <c>Person</c> → <c>People</c>)
/// or when the default derived name does not match convention.
/// </remarks>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
public sealed class UnitOfWorkPropertyAttribute(string propertyName) : Attribute
{
    /// <summary>The property name to use in the generated Unit of Work interface and class.</summary>
    public string PropertyName { get; } = propertyName;
}
