using System;

namespace DocApi.Domain.Entities
{
    public class IndicatorValue
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int IndicatorId { get; set; }
        public required string PeriodLabel { get; set; }
        public decimal MeasuredValue { get; set; }
        public string? Comment { get; set; }
        public DateTime MeasuredAt { get; set; }
        public int EnteredByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
