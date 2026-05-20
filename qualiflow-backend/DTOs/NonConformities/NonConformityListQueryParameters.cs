namespace DocApi.DTOs.NonConformities
{
    public class NonConformityListQueryParameters
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? Severity { get; set; }
        public int? ProcessId { get; set; }
        public int? ResponsibleUserId { get; set; }
        public int? OrganizationId { get; set; }
    }
}
