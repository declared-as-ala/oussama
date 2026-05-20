using System.Collections.Generic;
using DocApi.DTOs.Organizations;

namespace DocApi.DTOs.Dashboard
{
    public class DashboardChartResponse
    {
        public List<DashboardChartDataPointResponse> OrganizationsByStatus { get; set; } = new();
        public List<DashboardChartDataPointResponse> OrganizationsByType { get; set; } = new();
        public List<DashboardChartDataPointResponse> UsersByRole { get; set; } = new();
        public List<TopOrganizationResponse> TopOrganizationsByUsers { get; set; } = new();
        public List<TopOrganizationResponse> TopOrganizationsByDocuments { get; set; } = new();
        public List<TopOrganizationResponse> TopOrganizationsByNonConformities { get; set; } = new();
        public List<DashboardMonthlyTrendPointResponse> MonthlyOrganizationsCreated { get; set; } = new();
        public List<DashboardMonthlyTrendPointResponse> MonthlyUsersCreated { get; set; } = new();
        public List<DashboardMonthlyTrendPointResponse> MonthlyNonConformities { get; set; } = new();
        public List<DashboardChartDataPointResponse> AlertIndicatorsByOrganization { get; set; } = new();
    }
}
