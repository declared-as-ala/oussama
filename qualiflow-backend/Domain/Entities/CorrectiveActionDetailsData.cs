using System;

namespace DocApi.Domain.Entities
{
    public class CorrectiveActionDetailsData
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int NonConformityId { get; set; }
        public string? NonConformityCode { get; set; }
        public string? NonConformityTitle { get; set; }
        public string? NonConformityDescription { get; set; }
        public required string Type { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public int ResponsibleUserId { get; set; }
        public string? ResponsibleFullName { get; set; }
        public string? ResponsibleEmail { get; set; }
        public DateTime DueDate { get; set; }
        public required string Status { get; set; }
        public DateTime? CompletionDate { get; set; }
        public bool? EffectivenessVerified { get; set; }
        public string? EffectivenessComment { get; set; }
        public int? ProofRecordId { get; set; }
        public string? ProofRecordCode { get; set; }
        public string? ProofRecordTitle { get; set; }
        public string? ProofRecordType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
