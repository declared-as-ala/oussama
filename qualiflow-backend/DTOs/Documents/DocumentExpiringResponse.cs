using System;

namespace DocApi.DTOs.Documents
{
    public class DocumentExpiringResponse
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? VersionNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int DaysToExpiry { get; set; }
        public string ExpirationState { get; set; } = "VALID";
        public int? OwnerUserId { get; set; }
        public string? OwnerFullName { get; set; }
    }
}
