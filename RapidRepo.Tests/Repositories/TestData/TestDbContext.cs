﻿using Microsoft.EntityFrameworkCore;

namespace RapidRepo.Tests.Repositories.TestData;

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
        modelBuilder.Entity<Company>().HasQueryFilter(e => e.DeletedAt == null);
    }
}
