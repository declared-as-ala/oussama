using System;

namespace DocApi.Domain.Entities
{
    public sealed class WebPushSubscription
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int? OrganizationId { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
        public string? UserAgent { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }
}
