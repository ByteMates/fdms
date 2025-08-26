namespace DocumentService.Models
{
    public class Document
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string MimeType { get; set; }
        public string DocumentTitle { get; set; }
        public string DocumentType { get; set; }
        public DateTime UploadedAt { get; set; }

        // Optional for linking
        public string LinkedEntityType { get; set; } // e.g., "Claim"
        public string LinkedEntityId { get; set; }   // e.g., Claim ID or custom reference
    }

}
