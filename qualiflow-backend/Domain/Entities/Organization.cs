using System;

namespace DocApi.Domain.Entities
{
    public class Organization
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Code { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? LogoPath { get; set; }
        public string Status { get; set; } = "ACTIF"; // ACTIF, SUSPENDUE
        public int SubscriptionDaysRemaining { get; set; } = 30;
        public bool SubscriptionMonitorEnabled { get; set; } = true;
        public DateTime? LastSubscriptionDecrementAt { get; set; }
        public bool SubscriptionExpiryAlertSent { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
