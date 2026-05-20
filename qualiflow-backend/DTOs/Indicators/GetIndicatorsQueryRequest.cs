namespace DocApi.DTOs.Indicators
{
    public class GetIndicatorsQueryRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public string? Status { get; set; }
        public int? ProcessId { get; set; }
        public string? MeasurementFrequency { get; set; }
        public int? ResponsibleUserId { get; set; }
        public bool? IsInAlert { get; set; }
    }
}
