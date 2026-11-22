using AwesomeAssertions;

namespace RapidRepo.Extensions.DependencyInjection.Tests.Architecture;

/// <summary>
/// The DI package is shared by every provider, so it must not depend on any of them; provider-specific registration
/// arrives through <see cref="IRapidRepoProvider"/>.
/// </summary>
public class DependencyInjectionAssemblyTests
{
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("RapidRepo.EntityFrameworkCore")]
    public void DependencyInjectionAssembly_DoesNotReferenceAProvider(string assemblyPrefix)
    {
        typeof(RapidRepoOptions).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Should().NotContain(name => name != null && name.StartsWith(assemblyPrefix, StringComparison.Ordinal));
    }
}
