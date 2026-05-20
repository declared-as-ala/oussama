namespace DocApi.DTOs.Indicators
{
    public class CreateIndicatorRequest
    {
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
        public required string Status { get; set; }
    }
}
