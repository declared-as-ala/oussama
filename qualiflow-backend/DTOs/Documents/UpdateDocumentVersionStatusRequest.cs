using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Documents
{
    public class UpdateDocumentVersionStatusRequest
    {
        [Required]
        [MaxLength(20)]
        public required string Status { get; set; }

        public string? RevisionComment { get; set; }
    }
}

