using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.DTOs.Dashboard;

namespace DocApi.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<SuperAdminDashboardResponse> GetSuperAdminDashboardAsync(DashboardQueryParameters query);
        Task<DashboardKpiResponse> GetKpisAsync(DashboardQueryParameters query);
        Task<DashboardChartResponse> GetChartsAsync(DashboardQueryParameters query);
        Task<List<DashboardAlertResponse>> GetAlertsAsync(DashboardQueryParameters query);
        Task<List<DashboardRecentActivityResponse>> GetRecentActivitiesAsync(DashboardQueryParameters query);
        Task<List<DashboardRecentActivityResponse>> GetOrganizationRecentActivitiesAsync(int organizationId);
        Task<List<TopOrganizationResponse>> GetTopOrganizationsAsync(DashboardQueryParameters query);
    }
}
