using System;

namespace DocApi.Domain.Entities
{
    public class NotificationEventMessage
    {
        public int? OrganizationId { get; set; }
        public int UserId { get; set; }
        public int? SenderId { get; set; }
        public required string Type { get; set; }
        public required string Category { get; set; }
        public required string Title { get; set; }
        public required string Message { get; set; }
        public string Priority { get; set; } = "MEDIUM";
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
        public string? RedirectUrl { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? ReferenceType { get; set; }
        public string? ReferenceId { get; set; }
        public string? ActionUrl { get; set; }
        public int? TriggeredByUserId { get; set; }
        public DateTime TriggeredAt { get; set; }
    }
}
