using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Documents
{
    public class UpdateDocumentStatusRequest
    {
        [Required]
        [MaxLength(20)]
        public required string Status { get; set; }

        [MaxLength(1000)]
        public string? RevisionComment { get; set; }
    }
}
