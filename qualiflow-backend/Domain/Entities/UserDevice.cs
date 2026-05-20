using System;

namespace DocApi.Domain.Entities
{
    public class UserDevice
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string DeviceToken { get; set; } = string.Empty;
        public string Platform { get; set; } = "unknown";
        public string? DeviceName { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
    }
}
