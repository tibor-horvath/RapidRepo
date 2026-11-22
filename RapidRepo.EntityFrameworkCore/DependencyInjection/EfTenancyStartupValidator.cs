using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RapidRepo.EntityFrameworkCore.Internal;
using RapidRepo.Extensions.DependencyInjection;
using RapidRepo.Tenancy;

namespace RapidRepo.EntityFrameworkCore.DependencyInjection;

/// <summary>
/// Validates the tenancy configuration of <typeparamref name="TContext"/>'s model at startup. The context is only
/// resolved when the startup validation runs, which <c>AddRapidRepo</c> sets up only when a tenant provider is
/// configured, and a model without tenant entities passes without any other work.
/// </summary>
/// <typeparam name="TContext">
/// The context to validate: the bound context, or the base <see cref="DbContext"/> for an unbound provider.
/// </typeparam>
internal sealed class EfTenancyStartupValidator<TContext> : IRapidRepoStartupValidator
    where TContext : DbContext
{
    public IReadOnlyList<string> Validate(IServiceProvider services)
    {
        // Unbound, nothing guarantees a base DbContext is registered; without one there is nothing to validate here,
        // and the repositories that need it fail on first resolve with their own message.
        var context = typeof(TContext) == typeof(DbContext)
            ? services.GetService<TContext>()
            : services.GetRequiredService<TContext>();

        if (context is null)
            return [];

        var result = TenancyValidator.Validate(context);

        if (!result.IsValid)
            return result.Errors.Select(Prefix).ToList();

        if (result.TenantKeyType is not { } keyType)
            return [];

        // The tenant must be resolvable; resolving the provider surfaces a missing registration now, not on the
        // first request. Reading the tenant itself is not checked: there is no request at startup.
        if (services.GetService(typeof(ICurrentTenantProvider<>).MakeGenericType(keyType)) is null)
            return [Prefix(TenancyMessages.MissingTenantProvider(keyType))];

        return [];
    }

    private static string Prefix(string problem) => $"{typeof(TContext).Name}: {problem}";
}
