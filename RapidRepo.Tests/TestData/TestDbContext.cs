using Microsoft.EntityFrameworkCore;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.TestData;

public class TestDbContext : DbContext
{
    internal DbSet<Employee> Employees { get; set; }
    internal DbSet<Company> Companies { get; set; }

    public TestDbContext(
        DbContextOptions<TestDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>().HasQueryFilter(e => e.DeletedAt == null && e.DeletedBy == null);
    }
}
