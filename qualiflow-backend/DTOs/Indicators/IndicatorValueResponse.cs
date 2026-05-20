using System;

namespace DocApi.DTOs.Indicators
{
    public class IndicatorValueResponse
    {
        public int Id { get; set; }
        public int IndicatorId { get; set; }
        public required string PeriodLabel { get; set; }
        public decimal MeasuredValue { get; set; }
        public string? Comment { get; set; }
        public DateTime MeasuredAt { get; set; }
        public int EnteredByUserId { get; set; }
        public string? EnteredByFullName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
