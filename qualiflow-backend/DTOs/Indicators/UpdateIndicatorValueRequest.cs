using System;

namespace DocApi.DTOs.Indicators
{
    public class UpdateIndicatorValueRequest
    {
        public required string PeriodLabel { get; set; }
        public decimal MeasuredValue { get; set; }
        public string? Comment { get; set; }
        public DateTime MeasuredAt { get; set; }
    }
}
