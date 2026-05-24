using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Documents
{
    public class CreateDocumentRequest
    {
        public int? ProcessId { get; set; }
        public int? ProcedureId { get; set; }
        public System.Collections.Generic.List<int>? ProcessIds { get; set; }
        public System.Collections.Generic.List<int>? ProcedureIds { get; set; }

        [Required]
        [MaxLength(50)]
        public required string Code { get; set; }

        [Required]
        [MaxLength(255)]
        public required string Title { get; set; }

        [Required]
        [MaxLength(30)]
        public required string Type { get; set; }

        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Keywords { get; set; }
        public string? Signature { get; set; }
        public int? OwnerUserId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
