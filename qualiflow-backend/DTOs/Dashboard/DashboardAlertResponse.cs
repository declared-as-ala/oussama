using System;

namespace DocApi.DTOs.Dashboard
{
    public class DashboardAlertResponse
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "INFO";
        public string? ReferenceId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
