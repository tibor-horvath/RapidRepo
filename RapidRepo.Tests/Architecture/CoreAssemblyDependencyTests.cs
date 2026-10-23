using System.Reflection;
using FluentAssertions;
using RapidRepo.Entities;

namespace RapidRepo.Tests.Architecture;

/// <summary>
/// Guards the provider-neutral core: the RapidRepo assembly must not depend on EF Core,
/// so that other persistence providers (e.g. Dapper) can implement its contracts.
/// </summary>
public class CoreAssemblyDependencyTests
{
    // TODO(#111): un-skip once the contracts are neutralised and the EF Core references are
    // removed from RapidRepo.csproj (Phase 2).
    private const string PendingProviderSplit = "RapidRepo still depends on EF Core until the provider split (#111, Phase 2).";

    private const string EfCoreAssemblyPrefix = "Microsoft.EntityFrameworkCore";

    private static readonly Assembly CoreAssembly = typeof(BaseEntity<>).Assembly;

    [Fact(Skip = PendingProviderSplit)]
    public void CoreAssembly_DoesNotReferenceEntityFrameworkCore()
    {
        var efReferences = CoreAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name is not null && name.StartsWith(EfCoreAssemblyPrefix, StringComparison.Ordinal))
            .ToList();

        efReferences.Should().BeEmpty(
            "the RapidRepo core must stay provider-neutral; EF Core types belong in RapidRepo.EntityFrameworkCore");
    }

    [Fact(Skip = PendingProviderSplit)]
    public void CorePublicApi_DoesNotExposeEntityFrameworkCoreTypes()
    {
        var offenders = CoreAssembly
            .GetExportedTypes()
            .SelectMany(type => PublicSignatureTypes(type).Select(used => (type, used)))
            .Where(pair => pair.used.Namespace?.StartsWith(EfCoreAssemblyPrefix, StringComparison.Ordinal) == true)
            .Select(pair => $"{pair.type.FullName} -> {pair.used.FullName}")
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty("no public type in RapidRepo may mention EF Core types");
    }

    private static IEnumerable<Type> PublicSignatureTypes(Type type)
    {
        const BindingFlags Visible = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var types = new List<Type>();
        if (type.BaseType is not null)
            types.Add(type.BaseType);
        types.AddRange(type.GetInterfaces());

        foreach (var member in type.GetMembers(Visible))
        {
            switch (member)
            {
                case MethodBase method when method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly:
                    if (method is MethodInfo info)
                        types.Add(info.ReturnType);
                    types.AddRange(method.GetParameters().Select(p => p.ParameterType));
                    break;
                case PropertyInfo property when property.GetMethod is { } getter
                    && (getter.IsPublic || getter.IsFamily || getter.IsFamilyOrAssembly):
                    types.Add(property.PropertyType);
                    break;
                case FieldInfo field when field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly:
                    types.Add(field.FieldType);
                    break;
            }
        }

        return types.SelectMany(Flatten);
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.HasElementType)
            return Flatten(type.GetElementType()!);
        if (type.IsGenericType)
            return type.GetGenericArguments().SelectMany(Flatten).Prepend(type.GetGenericTypeDefinition());
        return [type];
    }
}
