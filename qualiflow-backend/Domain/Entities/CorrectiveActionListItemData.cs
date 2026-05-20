using System;

namespace DocApi.Domain.Entities
{
    public class CorrectiveActionListItemData
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int NonConformityId { get; set; }
        public string? NonConformityCode { get; set; }
        public required string Type { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public int ResponsibleUserId { get; set; }
        public string? ResponsibleFullName { get; set; }
        public DateTime DueDate { get; set; }
        public required string Status { get; set; }
        public DateTime? CompletionDate { get; set; }
        public bool? EffectivenessVerified { get; set; }
        public int? ProofRecordId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
