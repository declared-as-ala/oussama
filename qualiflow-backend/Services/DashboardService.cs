using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocApi.DTOs.Dashboard;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;

namespace DocApi.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardRepository _dashboardRepository;
        private readonly IActionLogRepository _actionLogRepository;

        public DashboardService(IDashboardRepository dashboardRepository, IActionLogRepository actionLogRepository)
        {
            _dashboardRepository = dashboardRepository;
            _actionLogRepository = actionLogRepository;
        }

        public async Task<SuperAdminDashboardResponse> GetSuperAdminDashboardAsync(DashboardQueryParameters query)
        {
            var kpisTask = _dashboardRepository.GetKpisAsync(query);
            var chartsTask = _dashboardRepository.GetChartsAsync(query);
            var alertsTask = _dashboardRepository.GetAlertsAsync(query);
            var recentActivitiesTask = _dashboardRepository.GetRecentActivitiesAsync(query);
            var topOrganizationsTask = _dashboardRepository.GetTopOrganizationsAsync(query);

            await Task.WhenAll(kpisTask, chartsTask, alertsTask, recentActivitiesTask, topOrganizationsTask);

            return new SuperAdminDashboardResponse
            {
                Kpis = await kpisTask,
                Charts = await chartsTask,
                Alerts = await alertsTask,
                RecentActivities = await recentActivitiesTask,
                TopOrganizations = await topOrganizationsTask
            };
        }

        public Task<DashboardKpiResponse> GetKpisAsync(DashboardQueryParameters query)
        {
            return _dashboardRepository.GetKpisAsync(query);
        }

        public Task<DashboardChartResponse> GetChartsAsync(DashboardQueryParameters query)
        {
            return _dashboardRepository.GetChartsAsync(query);
        }

        public Task<List<DashboardAlertResponse>> GetAlertsAsync(DashboardQueryParameters query)
        {
            return _dashboardRepository.GetAlertsAsync(query);
        }

        public Task<List<DashboardRecentActivityResponse>> GetRecentActivitiesAsync(DashboardQueryParameters query)
        {
            return _dashboardRepository.GetRecentActivitiesAsync(query);
        }

        public async Task<List<DashboardRecentActivityResponse>> GetOrganizationRecentActivitiesAsync(int organizationId)
        {
            var logs = await _actionLogRepository.GetByOrganizationIdAsync(organizationId);
            return logs.Select(l => new DashboardRecentActivityResponse
            {
                Type = l.Module,
                ActionType = l.ActionType,
                Title = l.Title,
                Description = l.Description,
                CreatedAt = l.CreatedAt,
                ActorName = l.ActorName
            }).ToList();
        }

        public Task<List<TopOrganizationResponse>> GetTopOrganizationsAsync(DashboardQueryParameters query)
        {
            return _dashboardRepository.GetTopOrganizationsAsync(query);
        }
    }
}
