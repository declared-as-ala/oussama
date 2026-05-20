using System.Collections.Generic;

namespace DocApi.DTOs.Organizations
{
    public class PagedOrganizationsResponse
    {
        public int Total { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<OrganizationListItemResponse> Items { get; set; } = new();
    }
}
