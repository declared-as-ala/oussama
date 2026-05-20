using System;

namespace DocApi.Domain.Entities
{
    public class Indicator
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int ProcessId { get; set; }
        public required string Code { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public string? CalculationMethod { get; set; }
        public string? Unit { get; set; }
        public decimal TargetValue { get; set; }
        public decimal AlertThreshold { get; set; }
        public required string MeasurementFrequency { get; set; }
        public int ResponsibleUserId { get; set; }
        public string Status { get; set; } = "ACTIF";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
