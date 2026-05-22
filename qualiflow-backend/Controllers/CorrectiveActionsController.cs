using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.CorrectiveActions;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/corrective-actions")]
    [Authorize]
    public class CorrectiveActionsController : ControllerBase
    {
        private readonly ICorrectiveActionService _correctiveActionService;

        public CorrectiveActionsController(ICorrectiveActionService correctiveActionService)
        {
            _correctiveActionService = correctiveActionService;
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<PagedCorrectiveActionResponse>> GetAll([FromQuery] GetCorrectiveActionsQueryRequest query)
        {
            try
            {
                var result = await _correctiveActionService.GetCorrectiveActionsAsync(query, GetUserContext());
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
        public async Task<ActionResult<CorrectiveActionStatisticsResponse>> GetStatistics()
        {
            try
            {
                var result = await _correctiveActionService.GetStatisticsAsync(GetUserContext());
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
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<CorrectiveActionDetailsResponse>> GetById(int id)
        {
            try
            {
                var result = await _correctiveActionService.GetByIdAsync(id, GetUserContext());
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
        public async Task<ActionResult<CorrectiveActionResponse>> Create([FromBody] CreateCorrectiveActionRequest request)
        {
            try
            {
                var result = await _correctiveActionService.CreateAsync(request, GetUserContext());
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
        public async Task<ActionResult<CorrectiveActionResponse>> Update(int id, [FromBody] UpdateCorrectiveActionRequest request)
        {
            try
            {
                var result = await _correctiveActionService.UpdateAsync(id, request, GetUserContext());
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
                var deleted = await _correctiveActionService.DeleteAsync(id, GetUserContext());
                if (!deleted)
                {
                    return NotFound(new { message = "Action corrective introuvable." });
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

        [HttpPatch("{id:int}/status")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<CorrectiveActionResponse>> UpdateStatus(int id, [FromBody] UpdateCorrectiveActionStatusRequest request)
        {
            try
            {
                var result = await _correctiveActionService.UpdateStatusAsync(id, request, GetUserContext());
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

        [HttpPost("{id:int}/completion-notification")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<CorrectiveActionResponse>> NotifyCompletion(int id)
        {
            try
            {
                var result = await _correctiveActionService.NotifyCompletionAsync(id, GetUserContext());
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

        [HttpPatch("{id:int}/verify-effectiveness")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<CorrectiveActionResponse>> VerifyEffectiveness(int id, [FromBody] VerifyCorrectiveActionEffectivenessRequest request)
        {
            try
            {
                var result = await _correctiveActionService.VerifyEffectivenessAsync(id, request, GetUserContext());
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

        [HttpGet("by-nonconformity/{nonConformityId:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<List<CorrectiveActionListItemResponse>>> GetByNonConformity(int nonConformityId)
        {
            try
            {
                var result = await _correctiveActionService.GetByNonConformityIdAsync(nonConformityId, GetUserContext());
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

        [HttpGet("{id:int}/action-logs")]
        [HttpGet("{id:int}/history")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<List<CorrectiveActionActionLogResponse>>> GetHistory(int id)
        {
            try
            {
                var result = await _correctiveActionService.GetHistoryAsync(id, GetUserContext());
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
                var deleted = await _correctiveActionService.DeleteActionLogAsync(logId, GetUserContext());
                if (!deleted)
                {
                    return BadRequest(new { message = "Echec de suppression du log d'actions." });
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

        [HttpPost("{id:int}/attachments")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        [RequestSizeLimit(25_000_000)]
        public async Task<ActionResult<CorrectiveActionAttachmentResponse>> UploadAttachment(int id, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "Aucun fichier fourni." });
                }

                using var memoryStream = new System.IO.MemoryStream();
                await file.CopyToAsync(memoryStream);

                var result = await _correctiveActionService.AddAttachmentAsync(
                    id,
                    file.FileName,
                    file.ContentType,
                    memoryStream.ToArray(),
                    GetUserContext());

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

        [HttpGet("attachments/{attachmentId:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<IActionResult> GetAttachment(int attachmentId)
        {
            try
            {
                var attachment = await _correctiveActionService.GetAttachmentContentAsync(attachmentId, GetUserContext());
                if (attachment == null)
                {
                    return NotFound(new { message = "Piece jointe introuvable." });
                }

                return File(attachment.FileContent, attachment.MimeType ?? "application/octet-stream", attachment.OriginalFileName);
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

        [HttpDelete("attachments/{attachmentId:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<IActionResult> DeleteAttachment(int attachmentId)
        {
            try
            {
                var deleted = await _correctiveActionService.DeleteAttachmentAsync(attachmentId, GetUserContext());
                if (!deleted)
                {
                    return BadRequest(new { message = "Echec de suppression de la piece jointe." });
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
