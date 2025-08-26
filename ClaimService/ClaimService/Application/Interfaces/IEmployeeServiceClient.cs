namespace ClaimService.Application.Interfaces;

/// <summary>
/// Port for resolving EmployeeId using CNIC or Personnel Number from EmployeeService.
/// Implementation will call your existing EmployeeService API and forward JWT.
/// </summary>
public interface IEmployeeServiceClient
{
    /// <summary>
    /// Resolve EmployeeId by CNIC or Personnel Number. Returns null if not found.
    /// At least one of cnic/personnelNo should be provided.
    /// </summary>
    Task<string?> ResolveEmployeeIdAsync(string? cnic, string? personnelNo, CancellationToken ct);
}
