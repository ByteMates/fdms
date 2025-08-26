using System;
using System.Linq;
using ClaimService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public sealed class RowVersionTestInterceptor : SaveChangesInterceptor
{
    private static byte[] NewVersion() => BitConverter.GetBytes(DateTime.UtcNow.Ticks);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var ctx = eventData.Context;
        if (ctx is null) return base.SavingChanges(eventData, result);

        foreach (var e in ctx.ChangeTracker.Entries<Claim>()
                     .Where(e => e.State == EntityState.Added && (e.Entity.RowVersion == null || e.Entity.RowVersion.Length == 0)))
        {
            e.Entity.RowVersion = NewVersion();
        }

        return base.SavingChanges(eventData, result);
    }
}
