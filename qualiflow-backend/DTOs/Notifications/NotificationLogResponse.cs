using System;

namespace DocApi.DTOs.Notifications
{
    public class NotificationLogResponse
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int DocumentId { get; set; }
        public int? DocumentVersionId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public int? RecipientUserId { get; set; }
        public string RecipientRoleType { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string DeliveryStatus { get; set; } = string.Empty;
        public string? ExternalMessageId { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
