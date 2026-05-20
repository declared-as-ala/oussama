namespace DocApi.Domain.Entities
{
    public class DocumentDetailsData
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int? ProcessId { get; set; }
        public string? ProcessCode { get; set; }
        public string? ProcessName { get; set; }
        public int? ProcedureId { get; set; }
        public string? ProcedureCode { get; set; }
        public string? ProcedureTitle { get; set; }
        public required string Code { get; set; }
        public required string Title { get; set; }
        public required string Type { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Keywords { get; set; }
        public string? Signature { get; set; }
        public int? OwnerUserId { get; set; }
        public string? OwnerFullName { get; set; }
        public int? CurrentVersionId { get; set; }
        public bool IsActive { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public System.DateTime? UpdatedAt { get; set; }
    }
}
