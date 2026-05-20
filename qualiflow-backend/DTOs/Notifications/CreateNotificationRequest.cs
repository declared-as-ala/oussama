using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Notifications
{
    public sealed class CreateNotificationRequest
    {
        [Required]
        [MaxLength(80)]
        public required string Type { get; set; }

        [Required]
        [MaxLength(20)]
        public string Category { get; set; } = "INFO";

        [Required]
        [MaxLength(255)]
        public required string Title { get; set; }

        [Required]
        [MaxLength(5000)]
        public required string Message { get; set; }

        [MaxLength(20)]
        public string Priority { get; set; } = "MEDIUM";

        public int? OrganizationId { get; set; }
        public int? TargetUserId { get; set; }

        [MaxLength(80)]
        public string? TargetRole { get; set; }

        [MaxLength(100)]
        public string? SourceModule { get; set; }

        public int? ReferenceId { get; set; }

        [MaxLength(80)]
        public string? ReferenceType { get; set; }

        [MaxLength(500)]
        public string? RedirectUrl { get; set; }

        [MaxLength(500)]
        public string? ActionUrl { get; set; }
    }
}
