using System;

namespace DocApi.DTOs.Organizations
{
    public class OrganizationListItemResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string Status { get; set; } = "ACTIF";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? LogoPath { get; set; }
        public int SubscriptionDaysRemaining { get; set; }
        public bool SubscriptionMonitorEnabled { get; set; }
        public int UsersCount { get; set; }
        public int AdminsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
