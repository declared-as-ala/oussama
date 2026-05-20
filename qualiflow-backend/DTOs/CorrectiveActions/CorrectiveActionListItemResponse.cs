using System;

namespace DocApi.DTOs.CorrectiveActions
{
    public class CorrectiveActionListItemResponse
    {
        public int Id { get; set; }
        public int NonConformityId { get; set; }
        public string? NonConformityCode { get; set; }
        public required string Type { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public int ResponsibleUserId { get; set; }
        public string? ResponsibleFullName { get; set; }
        public DateTime DueDate { get; set; }
        public required string Status { get; set; }
        public bool IsOverdue { get; set; }
        public DateTime? CompletionDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
