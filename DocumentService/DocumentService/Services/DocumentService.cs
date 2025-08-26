using DocumentService.Data;
using DocumentService.DTOs;
using DocumentService.Models;
using DocumentService.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DocumentService.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public DocumentService(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<Document> UploadDocumentAsync(DocumentUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                throw new ArgumentException("No file uploaded");



            var uploadsFolder = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", request.LinkedEntityType, request.LinkedEntityId, "Docs");
            Directory.CreateDirectory(uploadsFolder);


            string fileName = Path.GetFileName(request.File.FileName);
            string filePath = Path.Combine(uploadsFolder, fileName);

            // ✅ Check if same file already exists for the same claim
            bool fileExists = await _context.Documents.AnyAsync(d =>
                d.FileName == fileName && d.LinkedEntityId == request.LinkedEntityId);

            if (fileExists)
                throw new InvalidOperationException("This file already exists for the claim.");

            //var uniqueFileName = $"{Guid.NewGuid()}_{request.File.FileName}";
            //var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            var document = new Document
            {
                DocumentTitle = request.Title,
                DocumentType = request.Type,
                FileName = request.File.FileName,
                FilePath = filePath.Replace(_environment.WebRootPath ?? "wwwroot", "").Replace("\\", "/"),
                MimeType = request.File.ContentType,
                LinkedEntityId = request.LinkedEntityId,
                LinkedEntityType = request.LinkedEntityType,
                UploadedAt = DateTime.UtcNow
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();
            return document;
        }

        public async Task<Document?> GetDocumentByIdAsync(string id)
        {
            return await _context.Documents.FirstOrDefaultAsync(d => d.Id.ToString() == id);
        }

        public async Task<List<Document>> SearchDocumentsAsync(string? id, string? title, string? type)
        {
            var query = _context.Documents.AsQueryable();

            if (!string.IsNullOrEmpty(id))
                query = query.Where(d => d.Id.ToString().Contains(id));

            if (!string.IsNullOrEmpty(title))
                query = query.Where(d => d.DocumentTitle.Contains(title));

            if (!string.IsNullOrEmpty(type))
                query = query.Where(d => d.DocumentType.Contains(type));

            return await query.ToListAsync();
        }

        public async Task<bool> DeleteDocumentAsync(string id)
        {
            var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id.ToString() == id);
            if (document == null) return false;

            var fullPath = Path.Combine(_environment.WebRootPath ?? "wwwroot", document.FilePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Document> UpdateOrReplaceDocumentAsync(int id, DocumentUpdateRequest request, IFormFile newFile = null)
        {

            var document = await _context.Documents.FindAsync(id);
            if (document == null)
                throw new KeyNotFoundException("Document not found.");

            // ✅ If new metadata is provided, update it
            if (!string.IsNullOrEmpty(request.DocumentTitle))
                document.DocumentTitle = request.DocumentTitle;

            if (!string.IsNullOrEmpty(request.DocumentType))
                document.DocumentType = request.DocumentType;

            if (!string.IsNullOrEmpty(request.LinkedEntityType))
                document.LinkedEntityType = request.LinkedEntityType;

            if (!string.IsNullOrEmpty(request.LinkedEntityId))
                document.LinkedEntityId = request.LinkedEntityId;


            if (newFile != null && newFile.Length > 0)
            {
                string uploadsRoot = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", document.LinkedEntityType, document.LinkedEntityId, "Docs");

                Directory.CreateDirectory(uploadsRoot);

                string newFileName = Path.GetFileName(newFile.FileName);
                string newFilePath = Path.Combine(uploadsRoot, newFileName);

                // ❗ Check for duplication (same file name for same Claim)
                bool fileExists = await _context.Documents.AnyAsync(d =>
                  d.Id != document.Id &&  // ✅ Skip current document
                  d.FileName == newFileName &&
                  d.LinkedEntityId == document.LinkedEntityId);

                if (fileExists)
                    throw new InvalidOperationException("A file with this name already exists for the same claim and cannot be overwritten.");

                // ✅ Replace file on disk
                using (var stream = new FileStream(newFilePath, FileMode.Create))
                {
                    await newFile.CopyToAsync(stream);
                }

                // ✅ Delete old file if it exists
                string oldFullPath = Path.Combine(_environment.WebRootPath, document.FilePath);
                if (System.IO.File.Exists(oldFullPath))
                {
                    System.IO.File.Delete(oldFullPath);
                }

                // ✅ Update file metadata
                document.FileName = newFileName;
                document.FilePath = Path.Combine("uploads", "Claims", document.LinkedEntityId, "Docs", newFileName);
                document.MimeType = newFile.ContentType;
                document.UploadedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return document;
        }
    }
}
