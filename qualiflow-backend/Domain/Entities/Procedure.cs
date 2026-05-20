using System;

namespace DocApi.Domain.Entities
{
    public class Procedure
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int ProcessId { get; set; }
        public required string Code { get; set; }
        public required string Title { get; set; }
        public string? Objective { get; set; }
        public string? Scope { get; set; }
        public string? Description { get; set; }
        public int? ResponsibleUserId { get; set; }
        public string Status { get; set; } = "ACTIF";
        public string VersionNumber { get; set; } = "1.0";
        public string? RevisionComment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
