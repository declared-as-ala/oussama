using System;

namespace DocApi.DTOs.CorrectiveActions
{
    public class UpdateCorrectiveActionRequest
    {
        public int NonConformityId { get; set; }
        public required string Type { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public int ResponsibleUserId { get; set; }
        public DateTime DueDate { get; set; }
        public required string Status { get; set; }
        public int? ProofRecordId { get; set; }
        public DateTime? CompletionDate { get; set; }
    }
}
