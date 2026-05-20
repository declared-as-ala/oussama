using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Notifications;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/alert-rules")]
    [Authorize]
    public class AlertRulesController : ControllerBase
    {
        private readonly IAlertRuleService _alertRuleService;

        public AlertRulesController(IAlertRuleService alertRuleService)
        {
            _alertRuleService = alertRuleService;
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<List<AlertRuleResponse>>> GetAll()
        {
            try
            {
                var result = await _alertRuleService.GetAllAsync(GetUserContext());
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

        [HttpGet("{id:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<AlertRuleResponse>> GetById(int id)
        {
            try
            {
                var result = await _alertRuleService.GetByIdAsync(id, GetUserContext());
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
        public async Task<ActionResult<AlertRuleResponse>> Create([FromBody] CreateAlertRuleRequest request)
        {
            try
            {
                var result = await _alertRuleService.CreateAsync(request, GetUserContext());
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
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<AlertRuleResponse>> Update(int id, [FromBody] UpdateAlertRuleRequest request)
        {
            try
            {
                var result = await _alertRuleService.UpdateAsync(id, request, GetUserContext());
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

        [HttpPatch("{id:int}/toggle-status")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<AlertRuleResponse>> ToggleStatus(int id)
        {
            try
            {
                var result = await _alertRuleService.ToggleStatusAsync(id, GetUserContext());
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
