namespace DocApi.DTOs.Dashboard
{
    public class TopOrganizationResponse
    {
        public int OrganizationId { get; set; }
        public string OrganizationName { get; set; } = string.Empty;
        public int UsersCount { get; set; }
        public int DocumentsCount { get; set; }
        public int NonConformitiesCount { get; set; }
    }
}
