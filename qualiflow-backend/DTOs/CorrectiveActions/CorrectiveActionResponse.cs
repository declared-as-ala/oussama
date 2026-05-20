using System;

namespace DocApi.DTOs.CorrectiveActions
{
    public class CorrectiveActionResponse
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
        public string? EffectivenessComment { get; set; }
        public int? ProofRecordId { get; set; }
        public bool IsOverdue { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
