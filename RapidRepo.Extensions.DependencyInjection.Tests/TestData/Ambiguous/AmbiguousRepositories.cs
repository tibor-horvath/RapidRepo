using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.Repositories;
using RapidRepo.Repositories.Interfaces;

namespace RapidRepo.Extensions.DependencyInjection.Tests.TestData.Ambiguous;

public class Gizmo : BaseEntity<int> { }

public interface IGizmoRepository : IRepository<Gizmo, int> { }

public class GizmoRepositoryA(DbContext db) : BaseRepository<Gizmo, int>(db), IGizmoRepository { }

public class GizmoRepositoryB(DbContext db) : BaseRepository<Gizmo, int>(db), IGizmoRepository { }
