using Microsoft.EntityFrameworkCore;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.TestData;

public class TestDbContext : DbContext
{
    internal DbSet<Employee> Employees { get; set; }

    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {

    }
}
