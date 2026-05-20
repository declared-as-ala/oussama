using System.Security.Claims;
using System.Threading.Tasks;
using Dapper;
using DocApi.Common;
using DocApi.DTOs.Organizations;
using DocApi.DTOs.Users;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrganizationsController : ControllerBase
    {
        private readonly IOrganizationService _organizationService;
        private readonly IUserService _userService;
        private readonly DocApi.Infrastructure.IDbConnectionFactory _connectionFactory;

        public OrganizationsController(IOrganizationService organizationService, IUserService userService, DocApi.Infrastructure.IDbConnectionFactory connectionFactory)
        {
            _organizationService = organizationService;
            _userService = userService;
            _connectionFactory = connectionFactory;
        }

        [HttpGet("requests")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<IEnumerable<DocApi.Domain.Entities.Notification>>> GetOrganizationRequests()
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var sql = @"
                    SELECT * 
                    FROM Notifications 
                    WHERE ReferenceType = 'ORGANIZATION_REQUEST' 
                    ORDER BY CreatedAt DESC";
                
                var results = await connection.QueryAsync<DocApi.Domain.Entities.Notification>(sql);
                return Ok(results);
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("requests/{id:int}")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<IActionResult> DeleteOrganizationRequest(int id)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var sql = @"
                    DELETE FROM Notifications 
                    WHERE Id = @Id AND ReferenceType = 'ORGANIZATION_REQUEST'";
                
                var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
                if (rowsAffected == 0)
                {
                    return NotFound(new { message = "Demande introuvable ou déjà traitée." });
                }
                return NoContent();
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<PagedOrganizationsResponse>> GetAll([FromQuery] OrganizationListQueryParameters query)
        {
            try
            {
                var result = await _organizationService.GetAllAsync(query);
                return Ok(result);
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<OrganizationResponse>> GetById(int id)
        {
            try
            {
                var result = await _organizationService.GetByIdAsync(id);
                return Ok(result);
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
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<int>> Create([FromBody] CreateOrganizationRequest request)
        {
            try
            {
                var id = await _organizationService.CreateAsync(request);
                return CreatedAtAction(nameof(GetById), new { id }, id);
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateOrganizationRequest request)
        {
            try
            {
                var result = await _organizationService.UpdateAsync(id, request);
                return result ? NoContent() : BadRequest(new { message = "Failed to update organization" });
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
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<OrganizationResponse>> ToggleStatus(int id, [FromBody] ToggleOrganizationStatusRequest? request)
        {
            try
            {
                var payload = request ?? new ToggleOrganizationStatusRequest();
                var result = await _organizationService.ToggleStatusAsync(id, payload);
                return Ok(result);
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
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _organizationService.DeleteAsync(id);
                return result ? NoContent() : BadRequest(new { message = "Failed to delete organization" });
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

        [HttpGet("{id:int}/users")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<UserListResponse>> GetUsersByOrganization(
            int id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            try
            {
                var org = await _organizationService.GetByIdAsync(id);
                if (org == null)
                {
                    return NotFound(new { message = "Organization not found" });
                }

                var result = await _userService.GetAllAsync(id, page, pageSize);
                return Ok(result);
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

        [HttpPost("{id:int}/users")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<int>> CreateUserInOrganization(int id, [FromBody] CreateUserRequest request)
        {
            try
            {
                if (request.Role == UserRoles.SUPER_ADMIN)
                {
                    throw new ForbiddenException("Attribution du role SUPER_ADMIN interdite via cette route.");
                }

                var org = await _organizationService.GetByIdAsync(id);
                if (org == null)
                {
                    return NotFound(new { message = "Organization not found" });
                }

                request.OrganizationId = id;
                request.Role = request.Role?.Trim() ?? UserRoles.UTILISATEUR;

                var createdId = await _userService.CreateAsync(request, GetUserContext().UserId);
                return CreatedAtAction(nameof(GetUsersByOrganization), new { id }, createdId);
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

        [HttpPatch("{organizationId:int}/users/{userId:int}/role")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<IActionResult> ChangeUserRoleInOrganization(
            int organizationId,
            int userId,
            [FromBody] ChangeUserRoleRequest request)
        {
            try
            {
                if (request.Role == UserRoles.SUPER_ADMIN)
                {
                    throw new ForbiddenException("Attribution du role SUPER_ADMIN interdite via cette route.");
                }

                var org = await _organizationService.GetByIdAsync(organizationId);
                if (org == null)
                {
                    return NotFound(new { message = "Organization not found" });
                }

                var targetUser = await _userService.GetByIdAsync(userId);
                if (targetUser.OrganizationId != organizationId)
                {
                    throw new ForbiddenException("L'utilisateur n'appartient pas a cette organisation.");
                }

                var updated = await _userService.ChangeRoleAsync(userId, request, null);
                return updated
                    ? NoContent()
                    : BadRequest(new { message = "Failed to change user role" });
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

        [HttpGet("my")]
        [Authorize(Roles = "ADMIN_ORG")]
        public async Task<ActionResult<OrganizationResponse>> GetMyOrganization()
        {
            try
            {
                var result = await _organizationService.GetMyOrganizationAsync(GetUserContext());
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

        [HttpPut("my")]
        [Authorize(Roles = "ADMIN_ORG")]
        public async Task<IActionResult> UpdateMyOrganization([FromBody] UpdateOrganizationRequest request)
        {
            try
            {
                var result = await _organizationService.UpdateMyOrganizationAsync(request, GetUserContext());
                return result ? NoContent() : BadRequest(new { message = "Failed to update organization profile" });
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

        [HttpPost("my/logo")]
        [Authorize(Roles = "ADMIN_ORG")]
        public async Task<ActionResult<OrganizationLogoResponse>> UploadMyLogo([FromForm] UploadOrganizationLogoRequest request)
        {
            try
            {
                var result = await _organizationService.UploadMyLogoAsync(request, GetUserContext());
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

        [HttpGet("my/logo")]
        [Authorize(Roles = "ADMIN_ORG,RESPONSABLE_QUALITE,CHEF_SERVICE,UTILISATEUR")]
        public async Task<IActionResult> GetMyLogo()
        {
            try
            {
                var result = await _organizationService.GetMyOrganizationLogoAsync(GetUserContext());
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

        [HttpGet("{id:int}/logo")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG")]
        public async Task<IActionResult> GetLogo(int id)
        {
            try
            {
                var result = await _organizationService.GetOrganizationLogoAsync(id, GetUserContext());
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
