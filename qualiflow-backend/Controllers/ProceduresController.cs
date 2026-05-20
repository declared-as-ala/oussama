using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Procedures;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProceduresController : ControllerBase
    {
        private readonly IProcedureService _procedureService;

        public ProceduresController(IProcedureService procedureService)
        {
            _procedureService = procedureService;
        }

        [HttpGet]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,CHEF_SERVICE,UTILISATEUR")]
        public async Task<ActionResult<PagedProcedureResponse>> GetProcedures([FromQuery] ProcedureListQueryParameters query)
        {
            try
            {
                var result = await _procedureService.GetProceduresAsync(query, GetUserContext());
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
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,CHEF_SERVICE,UTILISATEUR")]
        public async Task<ActionResult<ProcedureStatisticsResponse>> GetStatistics()
        {
            try
            {
                var result = await _procedureService.GetStatisticsAsync(GetUserContext());
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
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,CHEF_SERVICE,UTILISATEUR")]
        public async Task<ActionResult<List<ProcedureListItemResponse>>> GetByProcess(int processId)
        {
            try
            {
                var result = await _procedureService.GetByProcessIdAsync(processId, GetUserContext());
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

        [HttpPost("by-process/{processId:int}/link/{procedureId:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult> AddProcessLink(int processId, int procedureId)
        {
            try
            {
                var result = await _procedureService.AddProcessLinkAsync(processId, procedureId, GetUserContext());
                if (!result)
                {
                    return BadRequest(new { message = "Impossible d'associer la procédure au processus." });
                }
                return Ok(new { message = "Procédure associée avec succès." });
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

        [HttpDelete("by-process/{processId:int}/link/{procedureId:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult> RemoveProcessLink(int processId, int procedureId)
        {
            try
            {
                var result = await _procedureService.RemoveProcessLinkAsync(processId, procedureId, GetUserContext());
                if (!result)
                {
                    return BadRequest(new { message = "Impossible de délier la procédure du processus." });
                }
                return Ok(new { message = "Procédure déliée avec succès." });
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
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,CHEF_SERVICE,UTILISATEUR")]
        public async Task<ActionResult<ProcedureDetailsResponse>> GetById(int id)
        {
            try
            {
                var result = await _procedureService.GetByIdAsync(id, GetUserContext());
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
        public async Task<ActionResult<ProcedureResponse>> Create([FromBody] CreateProcedureRequest request)
        {
            try
            {
                var result = await _procedureService.CreateAsync(request, GetUserContext());
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
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,CHEF_SERVICE,UTILISATEUR")]
        public async Task<ActionResult<ProcedureResponse>> Update(int id, [FromBody] UpdateProcedureRequest request)
        {
            try
            {
                var result = await _procedureService.UpdateAsync(id, request, GetUserContext());
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
                var deleted = await _procedureService.DeleteAsync(id, GetUserContext());
                if (!deleted)
                {
                    return BadRequest(new { message = "Echec de suppression de la procedure." });
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
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<ProcedureResponse>> ToggleStatus(int id)
        {
            try
            {
                var result = await _procedureService.ToggleStatusAsync(id, GetUserContext());
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

        [HttpGet("{procedureId:int}/instructions")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,CHEF_SERVICE,UTILISATEUR")]
        public async Task<ActionResult<List<InstructionResponse>>> GetInstructions(int procedureId)
        {
            try
            {
                var result = await _procedureService.GetInstructionsAsync(procedureId, GetUserContext());
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

        [HttpPost("{procedureId:int}/instructions")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<InstructionResponse>> CreateInstruction(int procedureId, [FromBody] CreateInstructionRequest request)
        {
            try
            {
                var result = await _procedureService.CreateInstructionAsync(procedureId, request, GetUserContext());
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

        [HttpPut("{procedureId:int}/instructions/{instructionId:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<InstructionResponse>> UpdateInstruction(int procedureId, int instructionId, [FromBody] UpdateInstructionRequest request)
        {
            try
            {
                var result = await _procedureService.UpdateInstructionAsync(procedureId, instructionId, request, GetUserContext());
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

        [HttpDelete("{procedureId:int}/instructions/{instructionId:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<IActionResult> DeleteInstruction(int procedureId, int instructionId)
        {
            try
            {
                var deleted = await _procedureService.DeleteInstructionAsync(procedureId, instructionId, GetUserContext());
                if (!deleted)
                {
                    return NotFound(new { message = "Instruction introuvable." });
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

        [HttpGet("{procedureId:int}/action-logs")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,CHEF_SERVICE,UTILISATEUR")]
        public async Task<ActionResult<List<ProcedureActionLogResponse>>> GetActionLogs(int procedureId)
        {
            try
            {
                var result = await _procedureService.GetActionLogsAsync(procedureId, GetUserContext());
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

        [HttpDelete("action-logs/{logId:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<IActionResult> DeleteActionLog(int logId)
        {
            try
            {
                var deleted = await _procedureService.DeleteActionLogAsync(logId, GetUserContext());
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
