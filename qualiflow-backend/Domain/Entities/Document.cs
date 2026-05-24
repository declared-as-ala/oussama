using System;

namespace DocApi.Domain.Entities
{
    public class Document
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int? ProcessId { get; set; }
        public int? ProcedureId { get; set; }
        public System.Collections.Generic.List<int> ProcessIds { get; set; } = new();
        public System.Collections.Generic.List<int> ProcedureIds { get; set; } = new();
        public required string Code { get; set; }
        public required string Title { get; set; }
        public required string Type { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Keywords { get; set; }
        public string? Signature { get; set; }
        public int? OwnerUserId { get; set; }
        public int? CurrentVersionId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? DeletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
