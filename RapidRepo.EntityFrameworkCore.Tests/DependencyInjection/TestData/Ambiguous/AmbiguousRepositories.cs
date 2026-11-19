using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.Repositories.Interfaces;

namespace RapidRepo.EntityFrameworkCore.Tests.DependencyInjection.TestData.Ambiguous;

public class Gizmo : BaseEntity<int> { }

public interface IGizmoRepository : IRepository<Gizmo, int> { }

public class GizmoRepositoryA(DbContext db) : EfRepository<Gizmo, int>(db), IGizmoRepository { }

public class GizmoRepositoryB(DbContext db) : EfRepository<Gizmo, int>(db), IGizmoRepository { }
