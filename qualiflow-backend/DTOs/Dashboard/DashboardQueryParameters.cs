namespace DocApi.DTOs.Dashboard
{
    public class DashboardQueryParameters
    {
        public string? Period { get; set; } = "12M";
        public int? OrganizationId { get; set; }
        public string? Status { get; set; }
        public string? Type { get; set; }
    }
}
