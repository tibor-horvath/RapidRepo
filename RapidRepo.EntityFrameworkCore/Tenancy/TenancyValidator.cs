using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RapidRepo.EntityFrameworkCore.Internal;
using RapidRepo.Extensions;
using RapidRepo.Tenancy;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// Checks that a context's tenant entities are fully isolated: the tenant filter is in place on every one of them,
/// nothing can replace it, and the context supplies the tenant.
/// </summary>
/// <remarks>
/// <para>
/// It runs on its own the first time a repository reads, or a unit of work commits, a tenant entity of a model
/// (once per model), and at host startup when <c>AddRapidRepo</c> configures tenancy. Call <see cref="Validate"/>
/// from a test to see the result, warnings included.
/// </para>
/// <para>A context without tenant entities always passes, without any other work.</para>
/// </remarks>
public static class TenancyValidator
{
    private static readonly ConditionalWeakTable<IModel, TenancyValidationResult> Results = new();

    /// <summary>
    /// Validates the tenancy configuration of <paramref name="context"/>'s model.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>Every problem found, and the warnings.</returns>
    public static TenancyValidationResult Validate(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValidateModel(context.Model, context.GetType());
    }

    /// <summary>
    /// Throws when <paramref name="context"/>'s model fails the validation. The result is cached per model, and
    /// warnings are traced once.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <exception cref="RapidRepoConfigurationException">The validation failed; <see cref="RapidRepoConfigurationException.Problems"/> lists every problem.</exception>
    public static void EnsureValid(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = Results.GetValue(context.Model, model =>
        {
            var validated = ValidateModel(model, context.GetType());
            foreach (var warning in validated.Warnings)
                Trace.TraceWarning($"[RapidRepo] {warning}");

            return validated;
        });

        result.ThrowIfInvalid();
    }

    internal static TenancyValidationResult ValidateModel(IModel model, Type contextType)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var tenantTypes = FindTenantTypes(model, errors);

        if (tenantTypes.Count == 0)
            return new TenancyValidationResult(contextType, tenantKeyType: null, errors, warnings);

        var keyType = CheckKeyTypes(contextType, tenantTypes, errors);

        foreach (var (entityType, _) in tenantTypes)
            CheckFilter(entityType, errors);

        var requireScopedForeignKeys = model.FindAnnotation(ModelBuilderExtensions.RequireTenantScopedForeignKeysAnnotation)?.Value is true;
        CheckForeignKeys(tenantTypes, requireScopedForeignKeys ? errors : warnings, requireScopedForeignKeys);

        return new TenancyValidationResult(contextType, keyType, errors, warnings);
    }

    /// <summary>
    /// The model's tenant entity types with their key types, in one pass. A type that implements
    /// <c>ITenantEntity&lt;TTenantKey&gt;</c> more than once is reported in <paramref name="errors"/>.
    /// </summary>
    private static List<(IEntityType EntityType, Type KeyType)> FindTenantTypes(IModel model, List<string> errors)
    {
        var tenantTypes = new List<(IEntityType, Type)>();

        foreach (var entityType in model.GetEntityTypes())
        {
            try
            {
                if (TenantTraits.GetTenantKeyType(entityType.ClrType) is { } keyType)
                    tenantTypes.Add((entityType, keyType));
            }
            catch (InvalidOperationException exception)
            {
                errors.Add(exception.Message);
            }
        }

        return tenantTypes;
    }

    /// <summary>
    /// Checks that every tenant entity shares one key type, which the context supplies.
    /// </summary>
    /// <returns>The shared key type, or <see langword="null"/> when the entities disagree.</returns>
    private static Type? CheckKeyTypes(Type contextType, List<(IEntityType EntityType, Type KeyType)> tenantTypes, List<string> errors)
    {
        var keyTypes = tenantTypes.Select(t => t.KeyType).Distinct().ToList();

        if (keyTypes.Count > 1)
        {
            errors.Add(TenancyMessages.MixedKeyTypes(contextType, tenantTypes.Select(t => (t.EntityType.ClrType, t.KeyType))));
            return null;
        }

        var keyType = keyTypes[0];

        if (!typeof(ITenantDbContext<>).MakeGenericType(keyType).IsAssignableFrom(contextType))
            errors.Add(TenancyMessages.MissingTenantDbContext(contextType, keyType));

        return keyType;
    }

    private static void CheckFilter(IEntityType entityType, List<string> errors)
    {
        var root = entityType.GetRootType();

        if (root != entityType)
        {
            if (!TenantTraits.IsTenantEntity(root.ClrType))
                errors.Add(TenancyMessages.RootNotTenantEntity(entityType.ClrType, root.ClrType));

            return;
        }

        if (entityType.GetDeclaredQueryFilters().Any(f => f.IsAnonymous))
        {
            errors.Add(TenancyMessages.AnonymousFilter(entityType.ClrType));
            return;
        }

        var filter = entityType.FindDeclaredQueryFilter(ModelBuilderExtensions.TenantFilterKey);

        if (filter is null)
            errors.Add(TenancyMessages.MissingTenantFilter(entityType.ClrType));
        else if (!IsTenantFilter(filter.Expression))
            errors.Add(TenancyMessages.ReplacedTenantFilter(entityType.ClrType));
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="filter"/> has the shape <c>AddTenantQueryFilters</c> gives it,
    /// <c>e =&gt; TenantQueryFilter.IsHostScope(c) || e.TenantId == TenantQueryFilter.CurrentTenantId(c)</c>, so a
    /// filter registered under the same key that does not restrict rows to the current tenant is caught.
    /// </summary>
    internal static bool IsTenantFilter(LambdaExpression? filter)
    {
        if (filter is not { Parameters: [var entity], Body: BinaryExpression { NodeType: ExpressionType.OrElse } body })
            return false;

        return IsCallTo(body.Left, nameof(TenantQueryFilter.IsHostScope))
            && body.Right is BinaryExpression { NodeType: ExpressionType.Equal } equal
            && ((IsTenantIdOf(equal.Left, entity) && IsCallTo(equal.Right, nameof(TenantQueryFilter.CurrentTenantId)))
                || (IsTenantIdOf(equal.Right, entity) && IsCallTo(equal.Left, nameof(TenantQueryFilter.CurrentTenantId))));
    }

    private static bool IsCallTo(Expression expression, string methodName) =>
        StripConvert(expression) is MethodCallExpression call
        && call.Method.DeclaringType == typeof(TenantQueryFilter)
        && call.Method.Name == methodName;

    private static bool IsTenantIdOf(Expression expression, ParameterExpression entity) =>
        StripConvert(expression) is MemberExpression { Member.Name: TenantTraits.TenantIdPropertyName } member
        && StripConvert(member.Expression!) == entity;

    private static Expression StripConvert(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert)
            expression = convert.Operand;

        return expression;
    }

    private static void CheckForeignKeys(
        List<(IEntityType EntityType, Type KeyType)> tenantTypes,
        List<string> problems,
        bool required)
    {
        foreach (var (entityType, _) in tenantTypes)
        {
            foreach (var foreignKey in entityType.GetDeclaredForeignKeys().Where(TenantForeignKeys.IsUnscoped))
            {
                problems.Add(TenancyMessages.UnscopedForeignKey(
                    entityType.ClrType,
                    foreignKey.PrincipalEntityType.ClrType,
                    foreignKey.Properties.Select(p => p.Name),
                    required));
            }
        }
    }
}
