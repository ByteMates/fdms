using ClaimService.Application.Interfaces;
using ClaimService.Domain.Entities;
using ClaimService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ClaimService.Infrastructure.Helpers;

public class IdGenerator : IIdGenerator
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;

    public IdGenerator(AppDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public async Task<string> NextClaimIdAsync(CancellationToken ct)
    {
        // Fiscal year config (defaults for PK: starts July 1)
        int startMonth = _cfg.GetValue("FiscalYear:StartMonth", 7);          // 1..12
        int startDay = _cfg.GetValue("FiscalYear:StartDay", 1);          // 1..31
        bool useRange = _cfg.GetValue("FiscalYear:UseRange", true);       // "2025-26" if true; "2025" if false
        string prefix = _cfg.GetValue("ClaimId:Prefix", "Claim");    // "Claim"
        int pad = _cfg.GetValue("ClaimId:Pad", 5);          // 00001
        string sep = _cfg.GetValue("ClaimId:Separator", "-");        // "-"

        var now = DateTime.UtcNow;
        var fy = GetFiscalYearString(now, startMonth, startDay, useRange);

        // Sequence key is per fiscal year
        var seqName = $"ClaimId:{fy}";
        var next = await NextFrom(seqName, ct);

        var formatted = $"{prefix}{sep}{fy}{sep}{next.ToString().PadLeft(pad, '0')}";
        return formatted;
    }

    public Task<long> NextQueueNoAsync(CancellationToken ct)
        => NextFrom("ClaimQueue", ct);

    private static string GetFiscalYearString(DateTime nowUtc, int startMonth, int startDay, bool useRange)
    {
        // Compute FY boundary for the given year
        var fyStartThisYear = new DateTime(nowUtc.Year, startMonth, Math.Min(startDay, DateTime.DaysInMonth(nowUtc.Year, startMonth)));
        var startYear = nowUtc >= fyStartThisYear ? nowUtc.Year : nowUtc.Year - 1;

        if (!useRange) return startYear.ToString();

        // e.g., 2025-26
        var endYearTwoDigits = (startYear + 1) % 100;
        return $"{startYear}-{endYearTwoDigits:D2}";
    }

    private async Task<long> NextFrom(string name, CancellationToken ct)
    {
        using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        var row = await _db.AppSequences.FirstOrDefaultAsync(s => s.Name == name, ct);
        if (row is null)
        {
            row = new AppSequence { Name = name, NextValue = 1 };
            _db.AppSequences.Add(row);
            await _db.SaveChangesAsync(ct);
        }

        var current = row.NextValue;
        row.NextValue = current + 1;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return current;
    }
}
