using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Documents;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/documents")]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IConversionService _conversionService;

        public DocumentsController(IDocumentService documentService, IConversionService conversionService)
        {
            _documentService = documentService;
            _conversionService = conversionService;
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<PagedDocumentResponse>> GetDocuments([FromQuery] DocumentListQueryRequest query)
        {
            try
            {
                var result = await _documentService.GetDocumentsAsync(query, GetUserContext());
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
        public async Task<ActionResult<DocumentStatisticsResponse>> GetStatistics()
        {
            try
            {
                var result = await _documentService.GetStatisticsAsync(GetUserContext());
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

        [HttpGet("expiring")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<List<DocumentExpiringResponse>>> GetExpiring([FromQuery] int withinDays = 30)
        {
            try
            {
                var result = await _documentService.GetExpiringAsync(withinDays, GetUserContext());
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

        [HttpGet("trash")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR,CHEF_SERVICE")]
        public async Task<ActionResult<PagedDocumentResponse>> GetTrash([FromQuery] DocumentListQueryRequest query)
        {
            try
            {
                var result = await _documentService.GetTrashAsync(query, GetUserContext());
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
        public async Task<ActionResult<DocumentDetailsResponse>> GetById(int id)
        {
            try
            {
                var result = await _documentService.GetByIdAsync(id, GetUserContext());
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
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<DocumentResponse>> Create([FromBody] CreateDocumentRequest request)
        {
            try
            {
                var result = await _documentService.CreateAsync(request, GetUserContext());
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<DocumentResponse>> Update(int id, [FromBody] UpdateDocumentRequest request)
        {
            try
            {
                var result = await _documentService.UpdateAsync(id, request, GetUserContext());
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

        [HttpPut("{id:int}/status")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<DocumentResponse>> UpdateStatus(int id, [FromBody] UpdateDocumentStatusRequest request)
        {
            try
            {
                var result = await _documentService.UpdateStatusAsync(id, request, GetUserContext());
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
                var deleted = await _documentService.DeleteAsync(id, GetUserContext());
                if (!deleted)
                {
                    return BadRequest(new { message = "Echec de suppression logique du document." });
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

        [HttpPatch("{id:int}/restore")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<IActionResult> Restore(int id)
        {
            try
            {
                var restored = await _documentService.RestoreAsync(id, GetUserContext());
                if (!restored)
                {
                    return BadRequest(new { message = "Echec de restauration du document." });
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

        [HttpDelete("{id:int}/permanent")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            try
            {
                var deleted = await _documentService.PermanentDeleteAsync(id, GetUserContext());
                if (!deleted)
                {
                    return BadRequest(new { message = "Echec de suppression definitive du document." });
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
        public async Task<ActionResult<DocumentResponse>> ToggleStatus(int id)
        {
            try
            {
                var result = await _documentService.ToggleStatusAsync(id, GetUserContext());
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

        [HttpGet("{documentId:int}/versions")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<List<DocumentVersionResponse>>> GetVersions(int documentId)
        {
            try
            {
                var result = await _documentService.GetVersionsAsync(documentId, GetUserContext());
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

        [HttpGet("{documentId:int}/action-logs")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<List<DocumentActionLogResponse>>> GetActionLogs(int documentId)
        {
            try
            {
                var result = await _documentService.GetActionLogsAsync(documentId, GetUserContext());
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

        [HttpPost("{documentId:int}/versions")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<DocumentVersionResponse>> CreateVersion(int documentId, [FromBody] CreateDocumentVersionRequest request)
        {
            try
            {
                var result = await _documentService.CreateVersionAsync(documentId, request, GetUserContext());
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

        [HttpPost("{documentId:int}/upload")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        [RequestSizeLimit(100_000_000)]
        public async Task<ActionResult<DocumentVersionResponse>> UploadVersion(int documentId, [FromForm] UploadDocumentVersionRequest request)
        {
            try
            {
                var result = await _documentService.UploadVersionAsync(documentId, request, GetUserContext());
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

        [HttpGet("{documentId:int}/versions/{versionId:int}")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<DocumentVersionResponse>> GetVersionById(int documentId, int versionId)
        {
            try
            {
                var result = await _documentService.GetVersionByIdAsync(documentId, versionId, GetUserContext());
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

        [HttpPatch("{documentId:int}/versions/{versionId:int}/status")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<DocumentVersionResponse>> UpdateVersionStatus(int documentId, int versionId, [FromBody] UpdateDocumentVersionStatusRequest request)
        {
            try
            {
                var result = await _documentService.UpdateVersionStatusAsync(documentId, versionId, request, GetUserContext());
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

        [HttpGet("{documentId:int}/download-current")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<IActionResult> DownloadCurrent(int documentId, [FromQuery] string? format = null)
        {
            try
            {
                var result = await _documentService.DownloadCurrentAsync(documentId, GetUserContext());
                if (!string.IsNullOrWhiteSpace(format))
                {
                    try
                    {
                        var conversion = await _conversionService.ConvertAsync(result.Stream, result.ContentType, result.FileName, format);
                        if (!ReferenceEquals(result.Stream, conversion.Stream))
                        {
                            result.Stream.Dispose();
                        }
                        return File(conversion.Stream, conversion.ContentType, conversion.FileName);
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new { message = $"Erreur de conversion: {ex.Message}" });
                    }
                }
                return File(result.Stream, result.ContentType, result.FileName);
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

        [HttpGet("{documentId:int}/versions/{versionId:int}/download")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<IActionResult> DownloadVersion(int documentId, int versionId, [FromQuery] string? format = null)
        {
            try
            {
                var result = await _documentService.DownloadVersionAsync(documentId, versionId, GetUserContext());
                if (!string.IsNullOrWhiteSpace(format))
                {
                    try
                    {
                        var conversion = await _conversionService.ConvertAsync(result.Stream, result.ContentType, result.FileName, format);
                        if (!ReferenceEquals(result.Stream, conversion.Stream))
                        {
                            result.Stream.Dispose();
                        }
                        return File(conversion.Stream, conversion.ContentType, conversion.FileName);
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new { message = $"Erreur de conversion: {ex.Message}" });
                    }
                }
                return File(result.Stream, result.ContentType, result.FileName);
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

        [HttpGet("{documentId:int}/preview-current")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<IActionResult> PreviewCurrent(int documentId)
        {
            try
            {
                var result = await _documentService.PreviewCurrentAsync(documentId, GetUserContext());
                return File(result.Stream, result.ContentType);
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
