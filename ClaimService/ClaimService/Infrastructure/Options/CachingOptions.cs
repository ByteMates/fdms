namespace ClaimService.Infrastructure.Options;

public sealed class CachingOptions
{
    public int EmployeeLookupMinutes { get; set; } = 10;
}
