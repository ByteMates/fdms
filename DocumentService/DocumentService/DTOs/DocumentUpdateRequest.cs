namespace DocumentService.DTOs
{
    public class DocumentUpdateRequest
    {
        public string? DocumentTitle { get; set; }
        public string? DocumentType { get; set; }
        public string? LinkedEntityType { get; set; }
        public string? LinkedEntityId { get; set; }
    }
}
