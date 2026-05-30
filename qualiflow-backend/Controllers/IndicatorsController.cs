using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Indicators;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/indicators")]
    [Authorize]
    public class IndicatorsController : ControllerBase
    {
        private readonly IIndicatorService _indicatorService;

        public IndicatorsController(IIndicatorService indicatorService)
        {
            _indicatorService = indicatorService;
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<PagedIndicatorResponse>> GetAll([FromQuery] GetIndicatorsQueryRequest query)
        {
            try
            {
                var result = await _indicatorService.GetIndicatorsAsync(query, GetUserContext());
                return Ok(result);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("statistics")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<IndicatorStatisticsResponse>> GetStatistics()
        {
            try
            {
                var result = await _indicatorService.GetStatisticsAsync(GetUserContext());
                return Ok(result);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("by-process/{processId:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<List<IndicatorListItemResponse>>> GetByProcess(int processId)
        {
            try
            {
                var result = await _indicatorService.GetByProcessAsync(processId, GetUserContext());
                return Ok(result);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("alerts")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<List<IndicatorAlertResponse>>> GetAlerts()
        {
            try
            {
                var result = await _indicatorService.GetAlertsAsync(GetUserContext());
                return Ok(result);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id:int}/chart")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<IndicatorChartResponse>> GetChart(int id)
        {
            try
            {
                var result = await _indicatorService.GetChartAsync(id, GetUserContext());
                return Ok(result);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<IndicatorDetailsResponse>> GetById(int id)
        {
            try
            {
                var result = await _indicatorService.GetByIdAsync(id, GetUserContext());
                return Ok(result);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<IndicatorResponse>> Create([FromBody] CreateIndicatorRequest request)
        {
            try
            {
                var result = await _indicatorService.CreateAsync(request, GetUserContext());
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<IndicatorResponse>> Update(int id, [FromBody] UpdateIndicatorRequest request)
        {
            try
            {
                var result = await _indicatorService.UpdateAsync(id, request, GetUserContext());
                return Ok(result);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _indicatorService.DeleteAsync(id, GetUserContext());
                if (!deleted)
                {
                    return NotFound(new { message = "Indicateur introuvable." });
                }

                return NoContent();
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPatch("{id:int}/toggle-status")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<IndicatorResponse>> ToggleStatus(int id)
        {
            try
            {
                var result = await _indicatorService.ToggleStatusAsync(id, GetUserContext());
                return Ok(result);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{indicatorId:int}/values")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<List<IndicatorValueResponse>>> GetValues(int indicatorId)
        {
            try
            {
                var result = await _indicatorService.GetValuesAsync(indicatorId, GetUserContext());
                return Ok(result);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{indicatorId:int}/values")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<IndicatorValueResponse>> CreateValue(int indicatorId, [FromBody] CreateIndicatorValueRequest request)
        {
            try
            {
                var result = await _indicatorService.CreateValueAsync(indicatorId, request, GetUserContext());
                return Ok(result);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{indicatorId:int}/values/{valueId:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<IndicatorValueResponse>> UpdateValue(int indicatorId, int valueId, [FromBody] UpdateIndicatorValueRequest request)
        {
            try
            {
                var result = await _indicatorService.UpdateValueAsync(indicatorId, valueId, request, GetUserContext());
                return Ok(result);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{indicatorId:int}/values/{valueId:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<IActionResult> DeleteValue(int indicatorId, int valueId)
        {
            try
            {
                var deleted = await _indicatorService.DeleteValueAsync(indicatorId, valueId, GetUserContext());
                if (!deleted)
                {
                    return NotFound(new { message = "Valeur indicateur introuvable." });
                }

                return NoContent();
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id:int}/action-logs")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<List<IndicatorActionLogResponse>>> GetActionLogs(int id)
        {
            try
            {
                var result = await _indicatorService.GetActionLogsAsync(id, GetUserContext());
                return Ok(result);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:int}/action-logs/{logId:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<IActionResult> DeleteActionLog(int id, int logId)
        {
            try
            {
                var deleted = await _indicatorService.DeleteActionLogAsync(logId, GetUserContext());
                if (!deleted)
                {
                    return NotFound(new { message = "Journal d'actions introuvable." });
                }

                return NoContent();
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private UserContext GetUserContext()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException("Utilisateur non authentifie.");
            }

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrWhiteSpace(role))
            {
                throw new UnauthorizedException("Role utilisateur introuvable.");
            }

            int? organizationId = null;
            var organizationClaim = User.FindFirst("OrganizationId")?.Value;
            if (int.TryParse(organizationClaim, out var parsedOrganizationId))
            {
                organizationId = parsedOrganizationId;
            }

            return new UserContext
            {
                UserId = userId,
                Role = role,
                OrganizationId = organizationId
            };
        }
    }
}
