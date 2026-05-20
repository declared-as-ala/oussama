using System;
using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Notifications
{
    public class NotificationLogRequest
    {
        [Required]
        [MaxLength(60)]
        public required string EventType { get; set; }

        [Required]
        public int DocumentId { get; set; }

        public int? DocumentVersionId { get; set; }
        public int? OrganizationId { get; set; }
        public int? RecipientUserId { get; set; }

        [MaxLength(30)]
        public string RecipientRoleType { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Channel { get; set; } = "EMAIL";

        [Required]
        [MaxLength(255)]
        public required string Subject { get; set; }

        [Required]
        [MaxLength(5000)]
        public required string Message { get; set; }

        [MaxLength(20)]
        public string DeliveryStatus { get; set; } = "SENT";

        [MaxLength(255)]
        public string? ExternalMessageId { get; set; }

        public string? PayloadJson { get; set; }
        public DateTime? SentAt { get; set; }
    }
}
