using System;

namespace DocApi.DTOs.NonConformities
{
    public class CorrectiveActionResponse
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int NonConformityId { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public int ResponsibleUserId { get; set; }
        public string? ResponsibleFullName { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public string Status { get; set; } = "A_FAIRE";
        public bool IsOverdue { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
