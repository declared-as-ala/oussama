using System;

namespace DocApi.DTOs.NonConformities
{
    public class UpdateCorrectiveActionRequest
    {
        public required string Title { get; set; }
        public string? Description { get; set; }
        public int ResponsibleUserId { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public string Status { get; set; } = "A_FAIRE";
    }
}
