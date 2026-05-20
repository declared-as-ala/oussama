using System;

namespace DocApi.DTOs.Procedures
{
    public class ProcedureListItemResponse
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int ProcessId { get; set; }
        public required string ProcessCode { get; set; }
        public required string ProcessName { get; set; }
        public required string Code { get; set; }
        public required string Title { get; set; }
        public int? ResponsibleUserId { get; set; }
        public string? ResponsibleFullName { get; set; }
        public string Status { get; set; } = "ACTIF";
        public string VersionNumber { get; set; } = "1.0";
        public DateTime CreatedAt { get; set; }
    }
}
