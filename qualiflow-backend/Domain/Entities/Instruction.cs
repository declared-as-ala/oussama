using System;

namespace DocApi.Domain.Entities
{
    public class Instruction
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int ProcedureId { get; set; }
        public required string Code { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = "ACTIF";
        public int OrderIndex { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
