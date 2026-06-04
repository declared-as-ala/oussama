using System.Collections.Generic;

namespace DocApi.DTOs.Procedures
{
    public class UpdateProcedureRequest
    {
        public List<int> ProcessIds { get; set; } = new();
        public required string Code { get; set; }
        public required string Title { get; set; }
        public string? Objective { get; set; }
        public string? Scope { get; set; }
        public string? Description { get; set; }
        public int? ResponsibleUserId { get; set; }
        public string Status { get; set; } = "ACTIF";
        public string VersionNumber { get; set; } = "1.0";
        public string? RevisionComment { get; set; }
    }
}
