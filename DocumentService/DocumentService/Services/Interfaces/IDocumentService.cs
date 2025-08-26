using DocumentService.DTOs;
using DocumentService.Models;


namespace DocumentService.Services.Interfaces
{
    public interface IDocumentService
    {
        Task<Document> UploadDocumentAsync(DocumentUploadRequest request);
        Task<Document?> GetDocumentByIdAsync(string id);
        Task<List<Document>> SearchDocumentsAsync(string? id, string? title, string? type);
        Task<bool> DeleteDocumentAsync(string id);

        Task<Document> UpdateOrReplaceDocumentAsync(int id, DocumentUpdateRequest request, IFormFile newFile = null);
    }

}
