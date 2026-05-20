using System.Collections.Generic;

namespace DocApi.DTOs.Dashboard
{
    public class SuperAdminDashboardResponse
    {
        public DashboardKpiResponse Kpis { get; set; } = new();
        public DashboardChartResponse Charts { get; set; } = new();
        public List<DashboardAlertResponse> Alerts { get; set; } = new();
        public List<DashboardRecentActivityResponse> RecentActivities { get; set; } = new();
        public List<TopOrganizationResponse> TopOrganizations { get; set; } = new();
    }
}
