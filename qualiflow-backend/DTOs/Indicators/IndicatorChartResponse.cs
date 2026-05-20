using System.Collections.Generic;

namespace DocApi.DTOs.Indicators
{
    public class IndicatorChartResponse
    {
        public List<string> Labels { get; set; } = new();
        public List<decimal> Values { get; set; } = new();
        public decimal TargetValue { get; set; }
        public decimal ThresholdValue { get; set; }
    }
}
