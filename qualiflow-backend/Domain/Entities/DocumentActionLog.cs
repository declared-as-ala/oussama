using System;

namespace DocApi.Domain.Entities
{
    public class DocumentActionLog
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int DocumentId { get; set; }
        public int? DocumentVersionId { get; set; }
        public required string ActionType { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Comment { get; set; }
        public int PerformedByUserId { get; set; }
        public DateTime PerformedAt { get; set; }
    }
}
