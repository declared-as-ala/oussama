using System;

namespace DocApi.DTOs.NonConformities
{
    public class NonConformityResponse
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public string? Code { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public required string Type { get; set; }
        public required string Severity { get; set; }
        public int? ProcessId { get; set; }
        public string? ProcessCode { get; set; }
        public string? ProcessName { get; set; }
        public int? ProcedureId { get; set; }
        public string? ProcedureCode { get; set; }
        public string? ProcedureTitle { get; set; }
        public int? ResponsibleUserId { get; set; }
        public string? ResponsibleFullName { get; set; }
        public DateTime DetectedDate { get; set; }
        public string Status { get; set; } = "EN_ATTENTE_VALIDATION";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
