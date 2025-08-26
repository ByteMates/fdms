// TestAppDbContext.cs  (in your ClaimServiceTest project)
using System.Linq;
using ClaimService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    private static byte[] NewVersion() => System.BitConverter.GetBytes(System.DateTime.UtcNow.Ticks);

    private void PopulateRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            // Look for a property literally named "RowVersion" (byte[])
            var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "RowVersion");
            if (prop is not null)
            {
                if (prop.CurrentValue is not byte[] bytes || bytes.Length == 0)
                    prop.CurrentValue = NewVersion();
            }
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PopulateRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PopulateRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
