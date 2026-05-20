using System;

namespace DocApi.Domain.Entities
{
    public class DocumentExpiringData
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? VersionNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int DaysToExpiry { get; set; }
        public int? OwnerUserId { get; set; }
        public string? OwnerFullName { get; set; }
    }
}
