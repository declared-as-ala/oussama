using System;

namespace DocApi.DTOs.Documents
{
    public class DocumentVersionResponse
    {
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public int OrganizationId { get; set; }
        public required string VersionNumber { get; set; }
        public required string Status { get; set; }
        public string? FileName { get; set; }
        public string? OriginalFileName { get; set; }
        public string? MimeType { get; set; }
        public long? FileSize { get; set; }
        public string? RevisionComment { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? Signature { get; set; }
        public bool IsCurrent { get; set; }
        public int EstablishedByUserId { get; set; }
        public string? EstablishedByUser { get; set; }
        public DateTime EstablishedAt { get; set; }
        public int? VerifiedByUserId { get; set; }
        public string? VerifiedByUser { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public int? ValidatedByUserId { get; set; }
        public string? ValidatedByUser { get; set; }
        public DateTime? ValidatedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

