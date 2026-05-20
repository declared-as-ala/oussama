using System;
using System.Collections.Generic;

namespace DocApi.DTOs.Organizations
{
    public class OrganizationResponse
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
        public required string Status { get; set; }
        public int SubscriptionDaysRemaining { get; set; }
        public bool SubscriptionMonitorEnabled { get; set; }
        public int UsersCount { get; set; }
        public int AdminsCount { get; set; }
        public List<OrganizationAdminSummaryResponse> Admins { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
