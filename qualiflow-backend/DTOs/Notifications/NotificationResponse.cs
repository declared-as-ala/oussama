using System;

namespace DocApi.DTOs.Notifications
{
    public class NotificationResponse
    {
        public int Id { get; set; }
        public int? OrganizationId { get; set; }
        public int UserId { get; set; }
        public int? SenderId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Priority { get; set; } = "MEDIUM";
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsPushSent { get; set; }
        public string Channel { get; set; } = "INAPP";
        public string? ExternalProviderId { get; set; }
        public bool IsArchived { get; set; }
        public int? DocumentId { get; set; }
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
        public string? SourceModule { get; set; }
        public string? RedirectUrl { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? ReferenceType { get; set; }
        public string? ReferenceId { get; set; }
        public string? ActionUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
