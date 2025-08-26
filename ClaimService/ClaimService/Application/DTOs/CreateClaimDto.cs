using System.ComponentModel.DataAnnotations;
using ClaimService.Domain.Enums;

namespace ClaimService.Application.Dtos;

public record CreateClaimDto(
    [param: Required] string EmployeeId,
    [param: Required] ClaimType ClaimType,
    [param: Required] DateTime ClaimDateUtc,
    [param: Range(0, double.MaxValue)] decimal AmountClaimed,
    string? HospitalCode);
