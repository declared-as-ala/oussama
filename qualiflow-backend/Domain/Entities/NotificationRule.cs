using System;

namespace DocApi.Domain.Entities
{
    public class NotificationRule
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string RoleType { get; set; } = string.Empty;
        public bool EmailEnabled { get; set; } = true;
        public bool InAppEnabled { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
