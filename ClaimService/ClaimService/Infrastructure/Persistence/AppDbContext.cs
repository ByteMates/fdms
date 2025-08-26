using ClaimService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimService.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimEvent> ClaimEvents => Set<ClaimEvent>();
    public DbSet<AppSequence> AppSequences => Set<AppSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Indexes for common queries
        modelBuilder.Entity<Claim>().HasIndex(x => x.EmployeeId);
        modelBuilder.Entity<Claim>().HasIndex(x => x.Status);
        modelBuilder.Entity<Claim>().HasIndex(x => x.ClaimDateUtc);
        modelBuilder.Entity<Claim>().HasIndex(x => x.QueueNo);

        modelBuilder.Entity<ClaimEvent>().HasIndex(x => x.ClaimId);

        // Seed starting sequence values
        modelBuilder.Entity<AppSequence>().HasData(
            new AppSequence { Name = "ClaimId", NextValue = 1 },
            new AppSequence { Name = "ClaimQueue", NextValue = 1 }
        );
    }
}
