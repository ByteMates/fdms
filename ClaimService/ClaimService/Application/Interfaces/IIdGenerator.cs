namespace ClaimService.Application.Interfaces;

public interface IIdGenerator
{
    /// <summary>Returns next ClaimId formatted like CLM-000001.</summary>
    Task<string> NextClaimIdAsync(CancellationToken ct);

    /// <summary>Returns next QueueNo for FIFO ordering.</summary>
    Task<long> NextQueueNoAsync(CancellationToken ct);
}
