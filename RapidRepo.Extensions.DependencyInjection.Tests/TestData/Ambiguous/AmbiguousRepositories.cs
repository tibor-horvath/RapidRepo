using RapidRepo.Entities;
using RapidRepo.Repositories.Interfaces;

namespace RapidRepo.Extensions.DependencyInjection.Tests.TestData.Ambiguous;

public class Gizmo : BaseEntity<int> { }

public interface IGizmoRepository : IRepository<Gizmo, int> { }

public class GizmoRepositoryA : StubRepository<Gizmo, int>, IGizmoRepository { }

public class GizmoRepositoryB : StubRepository<Gizmo, int>, IGizmoRepository { }
