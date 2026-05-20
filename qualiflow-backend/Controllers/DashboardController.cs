using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Dashboard;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        private bool TryGetCurrentOrganizationId(out int organizationId)
        {
            var orgIdClaim = User.FindFirstValue("OrganizationId");
            return int.TryParse(orgIdClaim, out organizationId);
        }

        [HttpGet("super-admin")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<SuperAdminDashboardResponse>> GetSuperAdminDashboard([FromQuery] DashboardQueryParameters query)
        {
            try
            {
                var response = await _dashboardService.GetSuperAdminDashboardAsync(query);
                return Ok(response);
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("super-admin/kpis")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<DashboardKpiResponse>> GetKpis([FromQuery] DashboardQueryParameters query)
        {
            try
            {
                return Ok(await _dashboardService.GetKpisAsync(query));
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("super-admin/charts")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<DashboardChartResponse>> GetCharts([FromQuery] DashboardQueryParameters query)
        {
            try
            {
                return Ok(await _dashboardService.GetChartsAsync(query));
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("super-admin/alerts")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<DashboardAlertResponse>> GetAlerts([FromQuery] DashboardQueryParameters query)
        {
            try
            {
                return Ok(await _dashboardService.GetAlertsAsync(query));
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("super-admin/recent-activities")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<DashboardRecentActivityResponse>> GetRecentActivities([FromQuery] DashboardQueryParameters query)
        {
            try
            {
                return Ok(await _dashboardService.GetRecentActivitiesAsync(query));
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("super-admin/top-organizations")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<TopOrganizationResponse>> GetTopOrganizations([FromQuery] DashboardQueryParameters query)
        {
            try
            {
                return Ok(await _dashboardService.GetTopOrganizationsAsync(query));
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("organization/recent-activities")]
        [Authorize(Roles = "ADMIN_ORG")]
        public async Task<ActionResult<List<DashboardRecentActivityResponse>>> GetOrganizationRecentActivities()
        {
            try
            {
                if (!TryGetCurrentOrganizationId(out var organizationId))
                {
                    return BadRequest(new { message = "L'utilisateur n'est pas associé à une organisation." });
                }

                return Ok(await _dashboardService.GetOrganizationRecentActivitiesAsync(organizationId));
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
