using Microsoft.EntityFrameworkCore;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.TestData;

public class TestDbContext : DbContext
{
    private bool _setQueryFilter;

    internal DbSet<Employee> Employees { get; set; }
    internal DbSet<Company> Companies { get; set; }

    public TestDbContext(
        DbContextOptions<TestDbContext> options,
        bool setQueryFilter = false)
        : base(options)
    {
        _setQueryFilter = setQueryFilter;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (_setQueryFilter)
        {
            modelBuilder.Entity<Employee>().HasQueryFilter(e => e.DeletedAt != null && e.DeletedBy != null);
        }
    }
}
