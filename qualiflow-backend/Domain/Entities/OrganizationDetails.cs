using System;

namespace DocApi.Domain.Entities
{
    public class OrganizationDetails
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Type { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? LogoPath { get; set; }
        public string Status { get; set; } = "ACTIF";
        public int SubscriptionDaysRemaining { get; set; }
        public bool SubscriptionMonitorEnabled { get; set; }
        public int UsersCount { get; set; }
        public int AdminsCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
