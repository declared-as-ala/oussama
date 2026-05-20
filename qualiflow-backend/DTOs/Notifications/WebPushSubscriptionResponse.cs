using System;

namespace DocApi.DTOs.Notifications
{
    public sealed class WebPushSubscriptionResponse
    {
        public int Id { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }
}
