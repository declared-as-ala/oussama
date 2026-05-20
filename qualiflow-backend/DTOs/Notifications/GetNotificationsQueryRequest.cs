using System;

namespace DocApi.DTOs.Notifications
{
    public class GetNotificationsQueryRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public bool? IsRead { get; set; }
        public string? Category { get; set; }
        public string? Priority { get; set; }
        public string? Type { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? OrganizationId { get; set; }
    }
}
