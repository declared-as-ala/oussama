namespace DocApi.DTOs.Documents
{
    public class DocumentListQueryRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
        public int? ProcessId { get; set; }
        public int? ProcedureId { get; set; }
        public int? OwnerUserId { get; set; }
        public int? OrganizationId { get; set; }
        public bool PendingValidationOnly { get; set; }
    }
}
