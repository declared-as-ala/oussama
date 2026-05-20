using System;
using System.Collections.Generic;
using DocApi.DTOs.Documents;
using DocApi.DTOs.Processes;

namespace DocApi.DTOs.Procedures
{
    public class ProcedureResponse
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int ProcessId { get; set; }
        public string? ProcessCode { get; set; }
        public string? ProcessName { get; set; }
        public required string Code { get; set; }
        public required string Title { get; set; }
        public string? Objective { get; set; }
        public string? Scope { get; set; }
        public string? Description { get; set; }
        public int? ResponsibleUserId { get; set; }
        public string? ResponsibleFullName { get; set; }
        public string Status { get; set; } = "ACTIF";
        public string VersionNumber { get; set; } = "1.0";
        public string? RevisionComment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public List<DocumentListItemResponse> Documents { get; set; } = new();
        public List<ProcessListItemResponse> Processes { get; set; } = new();
    }
}
