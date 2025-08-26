using System.ComponentModel.DataAnnotations;

namespace ClaimService.Application.Dtos;

public record UpdateClaimDto(
    [param: Range(0, double.MaxValue)] decimal? AmountClaimed,
    [param: Range(0, double.MaxValue)] decimal? AmountApproved,
    string? HospitalCode,
    byte[]? RowVersion); // <-- required to guard updates);
