using System;

namespace DocApi.DTOs.Documents
{
    public class DocumentActionLogResponse
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int DocumentId { get; set; }
        public int? DocumentVersionId { get; set; }
        public string? VersionNumber { get; set; }
        public required string ActionType { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Comment { get; set; }
        public int PerformedByUserId { get; set; }
        public string? PerformedByFullName { get; set; }
        public DateTime PerformedAt { get; set; }
    }
}
