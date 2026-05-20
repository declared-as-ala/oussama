using System.Collections.Generic;

namespace DocApi.DTOs.NonConformities
{
    public class PagedNonConformityResponse
    {
        public int Total { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<NonConformityListItemResponse> Items { get; set; } = new();
    }
}
