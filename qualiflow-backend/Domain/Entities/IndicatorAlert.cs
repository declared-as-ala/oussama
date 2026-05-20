using System;

namespace DocApi.Domain.Entities
{
    public class IndicatorAlert
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int IndicatorId { get; set; }
        public int IndicatorValueId { get; set; }
        public required string AlertType { get; set; }
        public required string Message { get; set; }
        public bool IsResolved { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
