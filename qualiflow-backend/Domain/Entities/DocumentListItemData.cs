using System;

namespace DocApi.Domain.Entities
{
    public class DocumentListItemData
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public required string Code { get; set; }
        public required string Title { get; set; }
        public required string Type { get; set; }
        public int? ProcessId { get; set; }
        public string? ProcessCode { get; set; }
        public string? ProcessName { get; set; }
        public int? ProcedureId { get; set; }
        public string? ProcedureCode { get; set; }
        public int? OwnerUserId { get; set; }
        public string? OwnerFullName { get; set; }
        public bool IsActive { get; set; }
        public string? Status { get; set; }
        public string? VersionNumber { get; set; }
        public string? FileName { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime? DeletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
