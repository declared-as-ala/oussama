using System;

namespace DocApi.DTOs.Indicators
{
    public class IndicatorAlertResponse
    {
        public int IndicatorId { get; set; }
        public string? IndicatorCode { get; set; }
        public string? IndicatorName { get; set; }
        public string? Message { get; set; }
        public decimal MeasuredValue { get; set; }
        public decimal TargetValue { get; set; }
        public decimal AlertThreshold { get; set; }
        public DateTime MeasuredAt { get; set; }
    }
}
