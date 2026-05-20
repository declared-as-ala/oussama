namespace DocApi.DTOs.Documents
{
    public class DocumentDetailsResponse
    {
        public required DocumentResponse Document { get; set; }
        public DocumentVersionResponse? CurrentVersion { get; set; }
        public List<DocumentVersionResponse> Versions { get; set; } = new();
    }
}

