using System;

namespace DocApi.Domain.Entities
{
    public class ProcedureListItemData
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
        public string? RevisionComment { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
