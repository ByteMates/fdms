namespace DocumentService.DTOs
{
    // DTOs/DocumentUploadResponse.cs
    public class DocumentUploadRequest
    {
        public string Title { get; set; }
        public string Type { get; set; }
        public string LinkedEntityType { get; set; } = "Claims";
        public string LinkedEntityId { get; set; } // e.g., CLM-001
        public IFormFile File { get; set; }
    }

}
