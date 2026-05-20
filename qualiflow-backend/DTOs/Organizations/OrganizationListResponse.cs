namespace DocApi.DTOs.Organizations
{
    public class OrganizationListResponse
    {
        public int Total { get; set; }
        public List<OrganizationListItemResponse> Items { get; set; } = new();
    }
}
