namespace DocApi.Domain.Entities
{
    public class IndicatorStatisticsData
    {
        public int ProcessId { get; set; }
        public string? ProcessName { get; set; }
        public string Status { get; set; } = "ACTIF";
        public string MeasurementFrequency { get; set; } = "MENSUEL";
        public bool IsInAlert { get; set; }
    }
}
