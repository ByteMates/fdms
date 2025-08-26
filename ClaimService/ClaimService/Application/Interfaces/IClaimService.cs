using System.Security.Claims;
using ClaimService.Application.Dtos;
using ClaimService.Domain.Entities;
using ClaimService.Domain.Enums;
using Claim = ClaimService.Domain.Entities.Claim;

namespace ClaimService.Application.Interfaces;

public interface IClaimService
{
    // Draft lifecycle
    Task<Claim> CreateDraftAsync(CreateClaimDto dto, string actorUserId, CancellationToken ct);
    Task<Claim> UpdateDraftAsync(string claimId, UpdateClaimDto dto, string actorUserId, CancellationToken ct);
    Task DeleteDraftAsync(string claimId, CancellationToken ct);

    // Generic transition (rules + dynamic RBAC inside the service)
    Task<Claim> GenericTransitionAsync(
        string claimId,
        ClaimStatus to,
        string? remarks,
        decimal? amountApproved,
        string actorUserId,
        ClaimsPrincipal user,
        CancellationToken ct);

    // Queries
    Task<(IEnumerable<Domain.Entities.Claim> items, int total)> SearchAsync(
        string? cnic, string? personnelNo, ClaimStatus? status,
        DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct);

    Task<IEnumerable<ClaimEvent>> GetEventsAsync(string claimId, CancellationToken ct);
    Task<IEnumerable<Claim>> GetFifoAsync(ClaimStatus stage, int take, CancellationToken ct);
}
