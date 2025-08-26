using ClaimService.Domain.Enums;

namespace ClaimService.Application.Dtos;

public record TransitionRequest(
    ClaimStatus ToStatus,
    string? Remarks,
    decimal? AmountApproved,
    byte[]? RowVersion = null // <-- default value
);

