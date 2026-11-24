using cl2j.Database.Benchmarks.Models;
using Microsoft.EntityFrameworkCore;

namespace cl2j.Database.Benchmarks;

/// <summary>The way EF Core is normally used: a mapped DbSet, a context per unit of work.</summary>
public sealed class BenchContext(DbContextOptions<BenchContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var client = modelBuilder.Entity<Client>();
        client.ToTable("Client");
        client.Property(c => c.Balance).HasPrecision(12, 2);
    }
}
