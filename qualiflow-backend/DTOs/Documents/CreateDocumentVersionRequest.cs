using System;
using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Documents
{
    public class CreateDocumentVersionRequest
    {
        [MaxLength(30)]
        public string? VersionNumber { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "BROUILLON";

        public string? RevisionComment { get; set; }

        [Required(ErrorMessage = "La date d'effet est obligatoire.")]
        public DateTime? EffectiveDate { get; set; }

        [Required(ErrorMessage = "La date d'expiration est obligatoire.")]
        public DateTime? ExpiryDate { get; set; }

        public string? Signature { get; set; }
    }
}
