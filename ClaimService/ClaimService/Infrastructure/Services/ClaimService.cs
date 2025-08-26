using System.Security.Claims;
using ClaimService.Application.Dtos;
using ClaimService.Application.Interfaces;
using ClaimService.Domain.Entities;
using ClaimService.Domain.Enums;
using ClaimService.Infrastructure.Helpers;
using ClaimService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Claim = ClaimService.Domain.Entities.Claim;

using System.Net;
using ClaimService.Application.Common.Errors;

namespace ClaimService.Infrastructure.Services;

public class ClaimService : IClaimService
{
    private readonly AppDbContext _db;
    private readonly IIdGenerator _ids;
    private readonly IEmployeeServiceClient _emp;


    // ----- Transition rule types -----
    private record TransitionContext(string? Remarks, decimal? AmountApproved, ClaimsPrincipal User);

    private sealed class TransitionRule
    {
        public required ClaimStatus From { get; init; }
        public required ClaimStatus To { get; init; }
        public string[] RequiredRoles { get; init; } = Array.Empty<string>();
        public Func<Claim, TransitionContext, CancellationToken, Task>? BeforeAsync { get; init; } // validate/mutate before change
        public Func<Claim, TransitionContext, CancellationToken, Task>? AfterAsync { get; init; } // optional side-effects
    }

    private readonly List<TransitionRule> _rules;

    public ClaimService(AppDbContext db, IIdGenerator ids, IEmployeeServiceClient emp)
    {
        _db = db;
        _ids = ids;
        _emp = emp;

        // ----- Define workflow once here -----
        _rules = new()
        {
            // Draft -> Submitted (assign queue)
            new TransitionRule {
                From = ClaimStatus.Draft, To = ClaimStatus.Submitted,
                RequiredRoles = new[] { PolicyConstants.MedicalRead,PolicyConstants.MedicalWrite},
                BeforeAsync = async (c, ctx, ct) =>
                {
                    if (!c.QueueNo.HasValue)
                        c.QueueNo = await _ids.NextQueueNoAsync(ct);
                }
            },

            // Submitted -> UnderHospitalReview
            new TransitionRule {
                From = ClaimStatus.Submitted, To = ClaimStatus.UnderHospitalReview,
                RequiredRoles = new[] { PolicyConstants.HospitalReview, PolicyConstants.MedicalWrite }
            },

            // UnderHospitalReview -> UnderSMBReview
            new TransitionRule {
                From = ClaimStatus.UnderHospitalReview, To = ClaimStatus.UnderSMBReview,
                RequiredRoles = new[] { PolicyConstants.HospitalReview, PolicyConstants.MedicalWrite }
            },

            // (Alt) Submitted -> UnderSMBReview
            new TransitionRule {
                From = ClaimStatus.Submitted, To = ClaimStatus.UnderSMBReview,
                RequiredRoles = new[] { PolicyConstants.MedicalRead,PolicyConstants.MedicalWrite, PolicyConstants .SmbDecide}
            },
 
            // UnderSMBReview -> Approved (requires valid AmountApproved)
            new TransitionRule {
                From = ClaimStatus.UnderSMBReview, To = ClaimStatus.Approved,
                RequiredRoles = new[] { PolicyConstants.SmbDecide, PolicyConstants.MedicalWrite},
                BeforeAsync = (c, ctx, ct) =>
                {
                    var errors = new Dictionary<string, string[]>();
                    if (ctx.AmountApproved is null)
                        errors["amountApproved"] = new[] { "AmountApproved is required to Approve." };
                    else
                    {
                        if (ctx.AmountApproved < 0)
                            errors["amountApproved"] = new[] { "AmountApproved cannot be negative." };
                        else if (ctx.AmountApproved > c.AmountClaimed)
                            errors["amountApproved"] = new[] { "AmountApproved must be between 0 and AmountClaimed." };
                        else
                            c.AmountApproved = ctx.AmountApproved;
                    }

                    if (errors.Count > 0) throw new ValidationException(errors);
                    return Task.CompletedTask;
                }
            },

            // UnderSMBReview -> Rejected
            new TransitionRule {
                From = ClaimStatus.UnderSMBReview, To = ClaimStatus.Rejected,
                RequiredRoles = new[] { PolicyConstants.SmbDecide, PolicyConstants.MedicalWrite},
            },

            // Return to Returned (from these stages)
            new TransitionRule {
                From = ClaimStatus.Submitted, To = ClaimStatus.Returned,
                RequiredRoles = new[] { PolicyConstants.MedicalRead,PolicyConstants.MedicalWrite},
            },
            new TransitionRule {
                From = ClaimStatus.UnderHospitalReview, To = ClaimStatus.Returned,
                RequiredRoles = new[]{ PolicyConstants.MedicalRead,PolicyConstants.MedicalWrite},
            },
            new TransitionRule {
                From = ClaimStatus.UnderSMBReview, To = ClaimStatus.Returned,
                RequiredRoles = new[] { PolicyConstants.MedicalRead,PolicyConstants.MedicalWrite}
            },
        };
    }

    // ===================== Draft CRUD =====================

    public async Task<Claim> CreateDraftAsync(CreateClaimDto dto, string actorUserId, CancellationToken ct)
    {
        var claim = new Claim
        {
            ClaimId = await _ids.NextClaimIdAsync(ct), // Claim-FY-00001 style
            EmployeeId = dto.EmployeeId,
            ClaimType = dto.ClaimType,
            ClaimDateUtc = dto.ClaimDateUtc,
            AmountClaimed = dto.AmountClaimed,
            HospitalCode = dto.HospitalCode,
            Status = ClaimStatus.Draft,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = DateTime.UtcNow,
            LastUpdatedAtUtc = DateTime.UtcNow
        };

        _db.Claims.Add(claim);
        await _db.SaveChangesAsync(ct);
        await AddEvent(claim.ClaimId, ClaimStatus.Draft, ClaimStatus.Draft, "Draft created", actorUserId, ct);
        return claim;
    }

    public async Task<Claim> UpdateDraftAsync(string claimId, UpdateClaimDto dto, string actorUserId, CancellationToken ct)
    {
        var c = await FindOrThrow(claimId, ct);
        EnsureOrBadRequest(c.Status == ClaimStatus.Draft, "Only Draft claims can be updated.");

        // Concurrency guard
        if (dto.RowVersion is null) ThrowValidation("rowVersion", "RowVersion is required.");
        _db.Entry(c).Property(x => x.RowVersion).OriginalValue = dto.RowVersion;


        var vErrors = new Dictionary<string, string[]>();

        if (dto.AmountClaimed.HasValue && dto.AmountClaimed.Value < 0)
            //  throw new InvalidOperationException("AmountClaimed cannot be negative.");
            vErrors["amountClaimed"] = new[] { "AmountClaimed cannot be negative." };
        if (dto.AmountApproved.HasValue && dto.AmountApproved.Value < 0)
            // throw new InvalidOperationException("AmountApproved cannot be negative.");
            vErrors["amountApproved"] = new[] { "AmountApproved cannot be negative." };
        if (dto.AmountApproved.HasValue && dto.AmountClaimed.HasValue &&
            dto.AmountApproved.Value > dto.AmountClaimed.Value)
           // throw new InvalidOperationException("AmountApproved cannot exceed AmountClaimed.");
            vErrors["amountApproved"] = new[] { "AmountApproved cannot exceed AmountClaimed." };
            
        if (vErrors.Count > 0) throw new ValidationException(vErrors);

        if (dto.AmountClaimed.HasValue) c.AmountClaimed = dto.AmountClaimed.Value;
        if (dto.AmountApproved.HasValue) c.AmountApproved = dto.AmountApproved.Value;
        if (dto.HospitalCode is not null) c.HospitalCode = dto.HospitalCode;

        c.LastUpdatedAtUtc = DateTime.UtcNow;
 
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            //throw new InvalidOperationException("The claim was modified by another user. Please refresh and retry.");

            throw new AppException(
                "The claim was modified by another user. Please refresh and retry.",
                HttpStatusCode.Conflict,
                errorCode: "ConcurrencyError");
        }


        await AddEvent(c.ClaimId, ClaimStatus.Draft, ClaimStatus.Draft, "Draft updated", actorUserId, ct);
        return c;
    }

    public async Task DeleteDraftAsync(string claimId, CancellationToken ct)
    {
        var c = await FindOrThrow(claimId, ct);
        EnsureOrBadRequest(c.Status == ClaimStatus.Draft, "Only Draft claims can be deleted.");
        _db.Claims.Remove(c);
        await _db.SaveChangesAsync(ct);
    }

    // ===================== Generic Transition =====================

    public async Task<Claim> GenericTransitionAsync(
        string claimId,
        ClaimStatus to,
        string? remarks,
        decimal? amountApproved,
        string actorUserId,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var c = await FindOrThrow(claimId, ct);

        var entry = _db.Entry(c);
        var original = entry.Property(x => x.RowVersion);
        if (original.OriginalValue is null)
            //throw new InvalidOperationException("RowVersion is required for transitions.");
            ThrowValidation("rowVersion", "RowVersion is required for transitions.");

        // Find a matching rule
        var rule = _rules.FirstOrDefault(r => r.From == c.Status && r.To == to)
                   ?? throw new AppException(
                        $"Transition {c.Status} → {to} is not allowed.",
                        HttpStatusCode.BadRequest,
                        errorCode: "InvalidTransition");

        // Dynamic RBAC check: does the caller have any of the required roles?
        var hasRole = rule.RequiredRoles.Any(user.IsInRole);
        if (!hasRole)
            throw new AppException(
               "Required role is missing for this transition.",
               HttpStatusCode.Forbidden,
               errorCode: "Forbidden");

        var ctx = new TransitionContext(remarks, amountApproved, user);

        // Pre-validation / side-effects
        if (rule.BeforeAsync is not null)
            await rule.BeforeAsync(c, ctx, ct);

        // State change
        var from = c.Status;
        c.Status = to;
        c.LastUpdatedAtUtc = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppException(
                "The claim was changed by someone else. Refresh and try again.",
                HttpStatusCode.Conflict,
                errorCode: "ConcurrencyError");
        }

        // Audit
        await AddEvent(c.ClaimId, from, to, remarks ?? $"{from} → {to}", actorUserId, ct);

        // Post side-effects (if any)
        if (rule.AfterAsync is not null)
            await rule.AfterAsync(c, ctx, ct);

        return c;
    }

    // ===================== Queries =====================

    //public async Task<(IEnumerable<Claim> items, int total)> SearchAsync(
    //    string? cnic, string? personnelNo, ClaimStatus? status,
    //    DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct)
    //{
    //    var q = _db.Claims.AsQueryable();

    //    // (Optional) Resolve EmployeeId via EmployeeService using CNIC/personnelNo and filter here.

    //    if (status.HasValue) q = q.Where(x => x.Status == status.Value);
    //    if (fromUtc.HasValue) q = q.Where(x => x.ClaimDateUtc >= fromUtc.Value);
    //    if (toUtc.HasValue) q = q.Where(x => x.ClaimDateUtc <= toUtc.Value);

    //    var total = await q.CountAsync(ct);
    //    var items = await q.OrderByDescending(x => x.ClaimDateUtc)
    //                       .Skip(Math.Max(0, (page - 1) * pageSize))
    //                       .Take(pageSize)
    //                       .ToListAsync(ct);
    //    return (items, total);
    //}

    public async Task<(IEnumerable<Claim> items, int total)> SearchAsync(
    string? cnic, string? personnelNo, ClaimStatus? status,
    DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct)
    {
        string? employeeId = null;
        if (!string.IsNullOrWhiteSpace(cnic))
            employeeId = await _emp.ResolveEmployeeIdAsync(cnic,null, ct);
        else if (!string.IsNullOrWhiteSpace(personnelNo))
            employeeId = await _emp.ResolveEmployeeIdAsync(null,personnelNo, ct);

        if ((cnic is not null || personnelNo is not null) && string.IsNullOrWhiteSpace(employeeId))
            return (Enumerable.Empty<Claim>(), 0);

        var q = _db.Claims.AsQueryable();
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(x => x.EmployeeId == employeeId);
        if (status.HasValue) q = q.Where(x => x.Status == status.Value);
        if (fromUtc.HasValue) q = q.Where(x => x.ClaimDateUtc >= fromUtc.Value);
        if (toUtc.HasValue) q = q.Where(x => x.ClaimDateUtc <= toUtc.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.ClaimDateUtc)
                           .Skip(Math.Max(0, (page - 1) * pageSize))
                           .Take(pageSize)
                           .ToListAsync(ct);
        return (items, total);
    }


    public async Task<IEnumerable<ClaimEvent>> GetEventsAsync(string claimId, CancellationToken ct)
        => await _db.ClaimEvents.Where(e => e.ClaimId == claimId)
                                .OrderBy(e => e.TimestampUtc)
                                .ToListAsync(ct);

    public async Task<IEnumerable<Claim>> GetFifoAsync(ClaimStatus stage, int take, CancellationToken ct)
        => await _db.Claims.Where(c => c.Status == stage && c.QueueNo.HasValue)
                           .OrderBy(c => c.QueueNo)
                           .Take(take)
                           .ToListAsync(ct);

    // ===================== Helpers =====================

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private async Task<Claim> FindOrThrow(string claimId, CancellationToken ct)
        => await _db.Claims.FirstOrDefaultAsync(c => c.ClaimId == claimId, ct)
           ?? throw new KeyNotFoundException($"Claim '{claimId}' not found.");

    private async Task AddEvent(string claimId, ClaimStatus from, ClaimStatus to, string remarks, string actor, CancellationToken ct)
    {
        _db.ClaimEvents.Add(new ClaimEvent
        {
            ClaimId = claimId,
            FromStatus = from,
            ToStatus = to,
            Remarks = remarks,
            ActorUserId = actor,
            TimestampUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }


    // Throw 400 with a single-field problem
    private static void ThrowValidation(string field, params string[] messages)
        => throw new ValidationException(new Dictionary<string, string[]>
        {
            [field] = messages.Length > 0 ? messages : new[] { "Invalid value." }
        });

    // Convert generic guard → 400 Bad Request (domain rule violations)
    private static void EnsureOrBadRequest(bool condition, string message)
    {
        if (!condition)
            throw new AppException(message, HttpStatusCode.BadRequest, errorCode: "RuleViolation");
    }
}
