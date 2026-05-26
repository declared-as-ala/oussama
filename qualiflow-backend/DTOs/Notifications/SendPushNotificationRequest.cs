using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Notifications
{
    public sealed class SendPushNotificationRequest
    {
        [Required]
        [MaxLength(255)]
        public required string Title { get; set; }

        [Required]
        [MaxLength(5000)]
        public required string Message { get; set; }

        public int? UserId { get; set; }
        public List<int>? UserIds { get; set; }

        // Backward-compatible aliases for older test clients.
        public string? ExternalId { get; set; }
        public List<string>? ExternalIds { get; set; }

        public int? OrganizationId { get; set; }
        public int? DocumentId { get; set; }
        public string? Type { get; set; }
        public string? RedirectUrl { get; set; }
    }
}
