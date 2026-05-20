using System;

namespace DocApi.Domain.Entities
{
    public class NotificationPreference
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public required string NotificationType { get; set; }
        public bool InAppEnabled { get; set; } = true;
        public bool EmailEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
