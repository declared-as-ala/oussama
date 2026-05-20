using System;

namespace DocApi.DTOs.Documents
{
    public class DocumentResponse
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int? ProcessId { get; set; }
        public string? ProcessCode { get; set; }
        public string? ProcessName { get; set; }
        public int? ProcedureId { get; set; }
        public string? ProcedureCode { get; set; }
        public string? ProcedureTitle { get; set; }
        public required string Code { get; set; }
        public required string Title { get; set; }
        public required string Type { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Keywords { get; set; }
        public string? Signature { get; set; }
        public int? OwnerUserId { get; set; }
        public string? OwnerFullName { get; set; }
        public int? CurrentVersionId { get; set; }
        public string? CurrentVersionNumber { get; set; }
        public string? CurrentVersionStatus { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
