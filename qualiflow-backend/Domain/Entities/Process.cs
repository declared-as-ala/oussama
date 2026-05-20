using System;

namespace DocApi.Domain.Entities
{
    public class Process
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public required string Code { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public required string Type { get; set; }
        public string? Finalities { get; set; }
        public string? Scope { get; set; }
        public string? Suppliers { get; set; }
        public string? Clients { get; set; }
        public string? InputData { get; set; }
        public string? OutputData { get; set; }
        public string? Objectives { get; set; }
        public int? PilotUserId { get; set; }
        public string Status { get; set; } = "ACTIF";
        public string VersionNumber { get; set; } = "1.0";
        public string? RevisionComment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
