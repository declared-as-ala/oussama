using System;

namespace DocApi.DTOs.CorrectiveActions
{
    public class GetCorrectiveActionsQueryRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? Type { get; set; }
        public int? ResponsibleUserId { get; set; }
        public int? NonConformityId { get; set; }
        public bool? IsOverdue { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
