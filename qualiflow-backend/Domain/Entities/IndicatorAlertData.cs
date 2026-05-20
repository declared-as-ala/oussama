using System;

namespace DocApi.Domain.Entities
{
    public class IndicatorAlertData
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int IndicatorId { get; set; }
        public string? IndicatorCode { get; set; }
        public string? IndicatorName { get; set; }
        public int IndicatorValueId { get; set; }
        public required string AlertType { get; set; }
        public required string Message { get; set; }
        public bool IsResolved { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public decimal MeasuredValue { get; set; }
        public DateTime MeasuredAt { get; set; }
        public decimal TargetValue { get; set; }
        public decimal AlertThreshold { get; set; }
    }
}
