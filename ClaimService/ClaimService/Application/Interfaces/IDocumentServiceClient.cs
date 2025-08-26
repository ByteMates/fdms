namespace ClaimService.Application.Interfaces;

/// <summary>
/// Optional port if you want pass-through operations to DocumentService from ClaimService.
/// Most teams keep DocumentService called directly by the UI using ClaimId as LinkedEntityId.
/// </summary>
public interface IDocumentServiceClient
{
    // Examples (implement later if needed):
    // Task<int> UploadAsync(string claimId, string title, string docType, IFormFile file, CancellationToken ct);
    // Task ReplaceAsync(int documentId, IFormFile file, CancellationToken ct);
    // Task UpdateMetadataAsync(int documentId, string? title, string? docType, CancellationToken ct);
}
