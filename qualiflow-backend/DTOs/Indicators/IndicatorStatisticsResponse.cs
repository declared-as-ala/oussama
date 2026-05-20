using System.Collections.Generic;

namespace DocApi.DTOs.Indicators
{
    public class IndicatorStatisticsResponse
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Inactive { get; set; }
        public int InAlert { get; set; }
        public Dictionary<string, int> ByFrequency { get; set; } = new();
        public Dictionary<string, int> ByProcess { get; set; } = new();
    }
}
