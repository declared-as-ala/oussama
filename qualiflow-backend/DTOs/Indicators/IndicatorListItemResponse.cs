using System;

namespace DocApi.DTOs.Indicators
{
    public class IndicatorListItemResponse
    {
        public int Id { get; set; }
        public int ProcessId { get; set; }
        public string? ProcessName { get; set; }
        public required string Code { get; set; }
        public required string Name { get; set; }
        public string? Unit { get; set; }
        public decimal TargetValue { get; set; }
        public decimal AlertThreshold { get; set; }
        public decimal? LatestValue { get; set; }
        public DateTime? LatestMeasuredAt { get; set; }
        public required string Status { get; set; }
        public string? ResponsibleFullName { get; set; }
        public bool IsInAlert { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
