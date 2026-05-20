using System;

namespace DocApi.DTOs.Processes
{
    public class ProcessActionLogResponse
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int ProcessId { get; set; }
        public required string ActionType { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Comment { get; set; }
        public int PerformedByUserId { get; set; }
        public string? PerformedByFullName { get; set; }
        public DateTime PerformedAt { get; set; }
    }
}
