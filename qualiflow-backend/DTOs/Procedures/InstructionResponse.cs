using System;

namespace DocApi.DTOs.Procedures
{
    public class InstructionResponse
    {
        public int Id { get; set; }
        public int ProcedureId { get; set; }
        public int OrganizationId { get; set; }
        public required string Code { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = "ACTIF";
        public int OrderIndex { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
