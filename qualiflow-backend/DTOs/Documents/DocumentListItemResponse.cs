using System;

namespace DocApi.DTOs.Documents
{
    public class DocumentListItemResponse
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public required string Code { get; set; }
        public required string Title { get; set; }
        public required string Type { get; set; }
        public int? ProcessId { get; set; }
        public string? ProcessCode { get; set; }
        public string? ProcessName { get; set; }
        public int? ProcedureId { get; set; }
        public string? ProcedureCode { get; set; }
        public System.Collections.Generic.List<int> ProcessIds { get; set; } = new();
        public System.Collections.Generic.List<int> ProcedureIds { get; set; } = new();
        public string Status { get; set; } = "BROUILLON";
        public string? VersionNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int? DaysToExpiry { get; set; }
        public string ExpirationState { get; set; } = "VALID";
        public DateTime UpdatedAt { get; set; }
        public int? OwnerUserId { get; set; }
        public string? OwnerFullName { get; set; }
        public string? FileName { get; set; }
        public bool IsActive { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int? DaysUntilPermanentDelete { get; set; }
    }
}
