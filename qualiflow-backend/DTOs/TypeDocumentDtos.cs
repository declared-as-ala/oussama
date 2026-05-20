using System;
using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs
{
    // DTO for creating a new document type
    public class CreateTypeDocumentRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // DTO for updating an existing document type
    public class UpdateTypeDocumentRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // DTO for returning document type information with user info
    public class TypeDocumentResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int CreatedByUserId { get; set; }
        public string? CreatedByUsername { get; set; }
        public int? UpdatedByUserId { get; set; }
        public string? UpdatedByUsername { get; set; }
    }
}
