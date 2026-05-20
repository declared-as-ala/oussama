using System;
using System.Collections.Generic;

namespace DocApi.DTOs.Processes
{
    public class ProcessResponse
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public required string Code { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public required string Type { get; set; }
        public List<string> Finalities { get; set; } = new();
        public List<string> Scope { get; set; } = new();
        public List<string> Suppliers { get; set; } = new();
        public List<string> Clients { get; set; } = new();
        public List<string> InputData { get; set; } = new();
        public List<string> OutputData { get; set; } = new();
        public List<string> Objectives { get; set; } = new();
        public int? PilotUserId { get; set; }
        public string? PilotFullName { get; set; }
        public string Status { get; set; } = "ACTIF";
        public string VersionNumber { get; set; } = "1.0";
        public string? RevisionComment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
