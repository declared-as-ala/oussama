namespace DocApi.DTOs.Processes
{
    public class ProcessListQueryParameters
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
        public int? PilotUserId { get; set; }
        public int? OrganizationId { get; set; }
        public bool? MyProcessesOnly { get; set; }
    }
}
