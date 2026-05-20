using System;

namespace DocApi.DTOs.Processes
{
    public class ProcessListItemResponse
    {
        public int Id { get; set; }
        public required string Code { get; set; }
        public required string Name { get; set; }
        public required string Type { get; set; }
        public string Status { get; set; } = "ACTIF";
        public int? PilotUserId { get; set; }
        public string? PilotFullName { get; set; }
        public int OrganizationId { get; set; }
        public string VersionNumber { get; set; } = "1.0";
        public DateTime CreatedAt { get; set; }
    }
}
