using System;

namespace DocApi.Domain.Entities
{
    public class Notification
    {
        public int Id { get; set; }
        public int? OrganizationId { get; set; }
        public int UserId { get; set; }
        public int? SenderId { get; set; }
        public required string Type { get; set; }
        public required string Category { get; set; }
        public required string Title { get; set; }
        public required string Message { get; set; }
        public string Priority { get; set; } = "MEDIUM";
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsPushSent { get; set; }
        public string Channel { get; set; } = "INAPP";
        public string? ExternalProviderId { get; set; }
        public bool IsArchived { get; set; }
        public Guid? PublicId { get; set; }
        public int? DocumentId { get; set; }
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
        public string? SourceModule { get; set; }
        public string? RedirectUrl { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? ReferenceType { get; set; }
        public string? ReferenceId { get; set; }
        public string? ActionUrl { get; set; }
        public string? TargetRole { get; set; }
        public bool EmailSent { get; set; }
        public DateTime? EmailSentAt { get; set; }
        public string? EmailError { get; set; }
        public int EmailAttemptCount { get; set; }
        public DateTime? EmailNextAttemptAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
