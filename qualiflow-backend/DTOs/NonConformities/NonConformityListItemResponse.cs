using System;

namespace DocApi.DTOs.NonConformities
{
    public class NonConformityListItemResponse
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public string? Code { get; set; }
        public required string Title { get; set; }
        public required string Type { get; set; }
        public required string Severity { get; set; }
        public int? ProcessId { get; set; }
        public string? ProcessCode { get; set; }
        public int? ProcedureId { get; set; }
        public string? ProcedureCode { get; set; }
        public int? ResponsibleUserId { get; set; }
        public string? ResponsibleFullName { get; set; }
        public DateTime DetectedDate { get; set; }
        public string Status { get; set; } = "OUVERTE";
        public DateTime CreatedAt { get; set; }
    }
}
