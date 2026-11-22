using AwesomeAssertions;
using RapidRepo.Tenancy;
using RapidRepo.Tenancy.Host;
using System.Diagnostics;

namespace RapidRepo.Tests.Tenancy;

/// <summary>
/// Tenant overrides and host scopes: they flow with the asynchronous context, nest, and restore the previous scope.
/// </summary>
public class TenantScopeTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private readonly TenantScope<Guid> _tenants = new();
    private readonly TenantHostScope _host = new();

    [Fact]
    public void Override_SetsTheTenant_AndDisposeRestoresNone()
    {
        using (_tenants.Override(TenantA))
            OverrideFor<Guid>().Should().Be(TenantA);

        AmbientTenant.TryGetOverride(typeof(Guid), out _).Should().BeFalse();
    }

    [Fact]
    public void Scopes_Nest_AndRestoreTheOuterOne()
    {
        using (_tenants.Override(TenantA))
        {
            using (_host.BeginHostScope())
            {
                AmbientTenant.IsHostScope.Should().BeTrue();
                AmbientTenant.TryGetOverride(typeof(Guid), out _).Should().BeFalse();

                using (_tenants.Override(TenantB))
                {
                    AmbientTenant.IsHostScope.Should().BeFalse();
                    OverrideFor<Guid>().Should().Be(TenantB);
                }

                AmbientTenant.IsHostScope.Should().BeTrue();
            }

            AmbientTenant.IsHostScope.Should().BeFalse();
            OverrideFor<Guid>().Should().Be(TenantA);
        }
    }

    [Fact]
    public void Override_AppliesOnlyToItsOwnKeyType()
    {
        using var scope = new TenantScope<string>().Override("acme");

        AmbientTenant.TryGetOverride(typeof(Guid), out _).Should().BeFalse();
        OverrideFor<string>().Should().Be("acme");
    }

    [Fact]
    public async Task Scopes_FlowAcrossAwaits_AndDoNotLeakToOtherFlows()
    {
        Guid? seenInOtherFlow = null;
        var otherFlow = new TaskCompletionSource();

        var background = Task.Run(async () =>
        {
            await otherFlow.Task;
            seenInOtherFlow = AmbientTenant.TryGetOverride(typeof(Guid), out var id) ? (Guid?)id : null;
        });

        using (_tenants.Override(TenantA))
        {
            await Task.Yield();
            OverrideFor<Guid>().Should().Be(TenantA);
            otherFlow.SetResult();
            await background;
        }

        seenInOtherFlow.Should().BeNull();
    }

    [Fact]
    public void DisposingOutOfOrder_Throws()
    {
        var outer = _tenants.Override(TenantA);
        var inner = _tenants.Override(TenantB);

        var disposeOuterFirst = outer.Dispose;

        disposeOuterFirst.Should().Throw<InvalidOperationException>().WithMessage("*out of order*");
        inner.Dispose();
        outer.Dispose();
        AmbientTenant.TryGetOverride(typeof(Guid), out _).Should().BeFalse();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var scope = _tenants.Override(TenantA);

        scope.Dispose();
        var again = scope.Dispose;

        again.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    public void Override_WithAnEmptyString_Throws(string tenantId)
    {
        var act = () => new TenantScope<string>().Override(tenantId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Override_WithTheDefaultValue_Throws()
    {
        var act = () => _tenants.Override(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HostScope_DeniedByThePolicy_Throws_AndOpensNothing()
    {
        TenantHostScopeRequest? asked = null;
        var host = new TenantHostScope(new Policy(r => { asked = r; return false; }));

        var begin = () => host.BeginHostScope();

        begin.Should().Throw<TenantViolationException>().WithMessage("*denied*");
        AmbientTenant.IsHostScope.Should().BeFalse();
        asked!.CallerMemberName.Should().Be(nameof(HostScope_DeniedByThePolicy_Throws_AndOpensNothing));
        asked.CallerFilePath.Should().EndWith("TenantScopeTests.cs");
    }

    [Fact]
    public void HostScope_TagsTheCurrentActivity()
    {
        using var activity = new Activity("test").Start();

        using (_host.BeginHostScope())
        {
        }

        activity.GetTagItem(TenantHostScope.HostScopeActivityTag).Should().Be(true);
        ((string)activity.GetTagItem(TenantHostScope.HostScopeCallerActivityTag)!).Should().Contain(nameof(HostScope_TagsTheCurrentActivity));
    }

    private static T OverrideFor<T>()
    {
        AmbientTenant.TryGetOverride(typeof(T), out var tenantId).Should().BeTrue();
        return (T)tenantId!;
    }

    private sealed class Policy(Func<TenantHostScopeRequest, bool> isAllowed) : ITenantHostScopePolicy
    {
        public bool IsAllowed(TenantHostScopeRequest request) => isAllowed(request);
    }
}
