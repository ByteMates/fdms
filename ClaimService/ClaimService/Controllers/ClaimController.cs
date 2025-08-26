using ClaimService.Application.Dtos;
using ClaimService.Application.Interfaces;
using ClaimService.Domain.Entities;
using ClaimService.Domain.Enums;
using ClaimService.Infrastructure.Helpers;
using ClaimService.Infrastructure.Persistence; // only for GetById shortcut
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Claim = ClaimService.Domain.Entities.Claim;

namespace ClaimService.Controllers;

[ApiController]
[Route("api/claims")]
public class ClaimController : ControllerBase
{
    private readonly IClaimService _svc;
    private readonly AppDbContext _db; // used only for GetById


    public ClaimController(IClaimService svc, AppDbContext db)
    {
        _svc = svc;
        _db = db;
    }

    // ---- helpers ----
    private string Actor() =>
        User.FindFirstValue("sub")
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.Identity?.Name
        ?? "system";

    // ===================== Draft CRUD =====================

    /// <summary>Create a new Draft claim.</summary>
    [HttpPost]
    [Authorize(Policy = PolicyConstants.MedicalWrite)]

    public async Task<ActionResult<Claim>> Create([FromBody] CreateClaimDto dto, CancellationToken ct)
    {
        var claim = await _svc.CreateDraftAsync(dto, Actor(), ct);
        return CreatedAtAction(nameof(GetById), new { claimId = claim.ClaimId }, claim);
    }

    /// <summary>Get a claim by ClaimId.</summary>
    [HttpGet("{claimId}")]
    [Authorize(Policy = PolicyConstants.MedicalRead)]
    public async Task<IActionResult> GetById([FromRoute] string claimId, CancellationToken ct)
    {
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.ClaimId == claimId, ct);
        return claim is null ? NotFound() : Ok(claim);
    }

    /// <summary>Update a Draft claim.</summary>
    [HttpPut("{claimId}")]
    [Authorize(Policy = PolicyConstants.MedicalWrite)]
    //public async Task<ActionResult<Claim>> UpdateDraft([FromRoute] string claimId, [FromBody] UpdateClaimDto dto, CancellationToken ct)
    //    => Ok(await _svc.UpdateDraftAsync(claimId, dto, Actor(), ct));

    public async Task<ActionResult<Claim>> UpdateDraft(string claimId, [FromBody] UpdateClaimDto dto, CancellationToken ct)
    {
        var result = await _svc.UpdateDraftAsync(claimId, dto, Actor(), ct);
        return Ok(result);
    }

    /// <summary>Delete a Draft claim.</summary>    
    [HttpDelete("{claimId}")]
    [Authorize(Policy = PolicyConstants.MedicalWrite)]
    public async Task<IActionResult> DeleteDraft([FromRoute] string claimId, CancellationToken ct)
    {
        await _svc.DeleteDraftAsync(claimId, ct);
        return NoContent();
    }

    // ===================== Generic Transition =====================

    /// <summary>
    /// Generic transition endpoint. RBAC & rules are enforced in the service.
    /// Body: { "toStatus": int, "remarks": "text", "amountApproved": decimal? }
    /// </summary>
    [HttpPost("{claimId}/transition")]
    [Authorize]
    public async Task<ActionResult<Claim>> Transition(string claimId, [FromBody] TransitionRequest req, CancellationToken ct)
    {
        // Pass RowVersion down to service
        var result = await _svc.GenericTransitionAsync(
            claimId, req.ToStatus, req.Remarks, req.AmountApproved, Actor(), User, ct);

        return Ok(result);
    }

    // ===================== Convenience endpoints (optional) =====================
    // These just call the generic transition with fixed targets and policy guards.
    // Keep them if your ops team prefers explicit actions.

    [HttpPost("{claimId}/submit")]
    [Authorize(Policy = PolicyConstants.MedicalWrite)]
    public Task<ActionResult<Claim>> Submit(string claimId, CancellationToken ct)
        => Transition(claimId, new TransitionRequest(ClaimStatus.Submitted, "Submitted", null), ct);

    [HttpPost("{claimId}/hospital-review")]
    [Authorize(Policy = PolicyConstants.MedicalRead)]
    public Task<ActionResult<Claim>> HospitalReview(string claimId, [FromBody] TransitionDto dto, CancellationToken ct)
        => Transition(claimId, new TransitionRequest(ClaimStatus.UnderHospitalReview, dto?.Remarks, null), ct);

    [HttpPost("{claimId}/hospital-verified")]
    [Authorize(Policy = PolicyConstants.MedicalWrite)]
    public Task<ActionResult<Claim>> HospitalVerified(string claimId, [FromBody] TransitionDto dto, CancellationToken ct)
        => Transition(claimId, new TransitionRequest(ClaimStatus.UnderSMBReview, dto?.Remarks, null), ct);

    [HttpPost("{claimId}/smb")]
    [Authorize(Policy = PolicyConstants.MedicalWrite)]
    public Task<ActionResult<Claim>> SendToSmb(string claimId, [FromBody] TransitionDto dto, CancellationToken ct)
        => Transition(claimId, new TransitionRequest(ClaimStatus.UnderSMBReview, dto?.Remarks, null), ct);

    [HttpPost("{claimId}/approve")]
    [Authorize(Policy = PolicyConstants.MedicalWrite)]
    public Task<ActionResult<Claim>> Approve(string claimId, [FromBody] UpdateClaimDto dto, CancellationToken ct)
        => Transition(claimId, new TransitionRequest(ClaimStatus.Approved, "Approved", dto.AmountApproved), ct);

    [HttpPost("{claimId}/reject")]
    [Authorize(Policy = PolicyConstants.MedicalWrite)]
    public Task<ActionResult<Claim>> Reject(string claimId, [FromBody] TransitionDto dto, CancellationToken ct)
        => Transition(claimId, new TransitionRequest(ClaimStatus.Rejected, dto?.Remarks, null), ct);

    [HttpPost("{claimId}/return")]
    [Authorize(Policy = PolicyConstants.MedicalWrite)]
    public Task<ActionResult<Claim>> Return(string claimId, [FromBody] TransitionDto dto, CancellationToken ct)
        => Transition(claimId, new TransitionRequest(ClaimStatus.Returned, dto?.Remarks, null), ct);

    // ===================== Queries =====================

    public record SearchRequest(
        string? Cnic,
        string? PersonnelNo,
        ClaimStatus? Status,
        DateTime? FromUtc,
        DateTime? ToUtc,
        int Page = 1,
        int PageSize = 50);

    [HttpPost("search")]
    [Authorize(Policy = PolicyConstants.MedicalRead)]
    public async Task<IActionResult> Search([FromBody] SearchRequest req, CancellationToken ct)
    {
        var (items, total) = await _svc.SearchAsync(req.Cnic, req.PersonnelNo, req.Status, req.FromUtc, req.ToUtc, req.Page, req.PageSize, ct);
        return Ok(new { total, items });
    }

    [HttpGet("{claimId}/events")]
    [Authorize(Policy = PolicyConstants.MedicalRead)]
    public async Task<ActionResult<IEnumerable<ClaimEvent>>> Events([FromRoute] string claimId, CancellationToken ct)
        => Ok(await _svc.GetEventsAsync(claimId, ct));

    [HttpGet("fifo/{stage}")]
    [Authorize(Policy = PolicyConstants.MedicalRead)]
    public async Task<ActionResult<IEnumerable<Claim>>> Fifo([FromRoute] ClaimStatus stage, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(await _svc.GetFifoAsync(stage, take, ct));
}
