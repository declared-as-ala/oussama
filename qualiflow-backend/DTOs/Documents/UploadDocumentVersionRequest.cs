using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DocApi.DTOs.Documents
{
    public class UploadDocumentVersionRequest
    {
        [Required]
        public IFormFile? File { get; set; }

        [MaxLength(30)]
        public string? VersionNumber { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "BROUILLON";

        public string? RevisionComment { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? Signature { get; set; }
    }
}

