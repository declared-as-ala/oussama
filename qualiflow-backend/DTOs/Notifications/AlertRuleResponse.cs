using System;

namespace DocApi.DTOs.Notifications
{
    public class AlertRuleResponse
    {
        public int Id { get; set; }
        public int? OrganizationId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string TriggerType { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public decimal? ThresholdValue { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
