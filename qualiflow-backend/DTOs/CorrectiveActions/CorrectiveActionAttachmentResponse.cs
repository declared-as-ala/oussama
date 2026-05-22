using System;

namespace DocApi.DTOs.CorrectiveActions
{
    public class CorrectiveActionAttachmentResponse
    {
        public int Id { get; set; }
        public int CorrectiveActionId { get; set; }
        public int OrganizationId { get; set; }
        public required string OriginalFileName { get; set; }
        public string? FileExtension { get; set; }
        public string? MimeType { get; set; }
        public long? FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
