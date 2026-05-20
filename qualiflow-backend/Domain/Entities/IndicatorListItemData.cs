using System;

namespace DocApi.Domain.Entities
{
    public class IndicatorListItemData
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int ProcessId { get; set; }
        public string? ProcessCode { get; set; }
        public string? ProcessName { get; set; }
        public required string Code { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public string? CalculationMethod { get; set; }
        public string? Unit { get; set; }
        public decimal TargetValue { get; set; }
        public decimal AlertThreshold { get; set; }
        public required string MeasurementFrequency { get; set; }
        public int ResponsibleUserId { get; set; }
        public string? ResponsibleFullName { get; set; }
        public string Status { get; set; } = "ACTIF";
        public decimal? LatestValue { get; set; }
        public DateTime? LatestMeasuredAt { get; set; }
        public bool IsInAlert { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
