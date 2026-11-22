using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using RapidRepo.Extensions.DependencyInjection.Tests.TestData;
using RapidRepo.Extensions.DependencyInjection.Tests.TestData.Ambiguous.Provider;

namespace RapidRepo.Extensions.DependencyInjection.Tests;

/// <summary>
/// Trace listeners are process-wide, so these tests run apart from every other test that may trace.
/// </summary>
[CollectionDefinition(nameof(TraceCollection), DisableParallelization = true)]
public sealed class TraceCollection;

/// <summary>
/// <c>AddRapidRepo</c> warns when it registers repositories or units of work without a provider, since nothing
/// then registers the data source they depend on.
/// </summary>
[Collection(nameof(TraceCollection))]
public sealed class MissingProviderWarningTests : IDisposable
{
    private readonly RecordingListener _listener = new();

    public MissingProviderWarningTests() => Trace.Listeners.Add(_listener);

    public void Dispose() => Trace.Listeners.Remove(_listener);

    [Fact]
    public void ScannedRepositories_WithoutProvider_Warn()
    {
        new ServiceCollection().AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.IncludeType<WidgetRepository>();
        });

        _listener.Messages.Should().Contain(m => IsMissingProviderWarning(m));
    }

    [Fact]
    public void UnitOfWork_WithoutProvider_Warns()
    {
        new ServiceCollection().AddRapidRepo(o => o.UseUnitOfWork<TestUnitOfWork>());

        _listener.Messages.Should().Contain(m => IsMissingProviderWarning(m));
    }

    [Fact]
    public void WithProvider_DoesNotWarn()
    {
        new ServiceCollection().AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.IncludeType<WidgetRepository>();
            o.UseProvider(new FakeProvider());
        });

        _listener.Messages.Should().NotContain(m => IsMissingProviderWarning(m));
    }

    [Fact]
    public void NothingRegistered_DoesNotWarn()
    {
        new ServiceCollection().AddRapidRepo(_ => { });

        _listener.Messages.Should().NotContain(m => IsMissingProviderWarning(m));
    }

    private static bool IsMissingProviderWarning(string message) =>
        message.Contains(RapidRepoServiceCollectionExtensions.MissingProviderMessage, StringComparison.Ordinal);

    private sealed class RecordingListener : TraceListener
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                    return [.. _messages];
            }
        }

        public override void Write(string? message) => Record(message);

        public override void WriteLine(string? message) => Record(message);

        private void Record(string? message)
        {
            if (message is null)
                return;

            lock (_messages)
                _messages.Add(message);
        }
    }
}
