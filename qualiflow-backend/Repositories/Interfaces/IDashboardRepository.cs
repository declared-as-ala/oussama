using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.DTOs.Dashboard;

namespace DocApi.Repositories.Interfaces
{
    public interface IDashboardRepository
    {
        Task<DashboardKpiResponse> GetKpisAsync(DashboardQueryParameters query);
        Task<DashboardChartResponse> GetChartsAsync(DashboardQueryParameters query);
        Task<List<DashboardAlertResponse>> GetAlertsAsync(DashboardQueryParameters query);
        Task<List<DashboardRecentActivityResponse>> GetRecentActivitiesAsync(DashboardQueryParameters query);
        Task<List<TopOrganizationResponse>> GetTopOrganizationsAsync(DashboardQueryParameters query, int limit = 10);
    }
}
