using Microsoft.Extensions.DependencyInjection;
using RapidRepo.Entities;
using RapidRepo.Repositories.Interfaces;

namespace RapidRepo.Extensions.DependencyInjection.Tests.TestData.Ambiguous.Provider;

// A provider's own repository interface, the way IEfRepository<,> is EF Core's. Every repository built on the
// provider implements it, so two repositories for one entity share it — ambiguous unless the provider is configured.
public interface IFakeProviderRepository<TEntity, in TKey> : IRepository<TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : notnull;

public class Sprocket : BaseEntity<int> { }

public interface ISprocketRepository : IRepository<Sprocket, int> { }

public interface IArchivedSprocketRepository : IRepository<Sprocket, int> { }

public class SprocketRepository : StubRepository<Sprocket, int>, IFakeProviderRepository<Sprocket, int>, ISprocketRepository { }

public class ArchivedSprocketRepository
    : StubRepository<Sprocket, int>, IFakeProviderRepository<Sprocket, int>, IArchivedSprocketRepository { }

/// <summary>Records what <c>AddRapidRepo</c> hands to a provider.</summary>
public sealed class FakeProvider : IRapidRepoProvider
{
    public string Name => "Fake";

    public IEnumerable<Type> AdditionalRootInterfaces => [typeof(IFakeProviderRepository<,>)];

    public List<RapidRepoRegistrationContext> Calls { get; } = [];

    public List<Type> ServiceTypesSeenAtRegistration { get; } = [];

    public void Register(IServiceCollection services, RapidRepoRegistrationContext context)
    {
        Calls.Add(context);
        ServiceTypesSeenAtRegistration.AddRange(services.Select(d => d.ServiceType));
    }
}
