using System;

namespace DocApi.Domain.Entities
{
    public class AlertRule
    {
        public int Id { get; set; }
        public int? OrganizationId { get; set; }
        public required string Code { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public required string EntityType { get; set; }
        public required string TriggerType { get; set; }
        public bool IsActive { get; set; } = true;
        public decimal? ThresholdValue { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
