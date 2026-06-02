using System;
using System.Security.Claims;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Users;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = UserRoles.ADMIN_ORG + "," + UserRoles.RESPONSABLE_QUALITE + "," + UserRoles.UTILISATEUR)]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException("User ID not found in token");
            }
            return userId;
        }

        private string? GetUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }

        private int? GetOrganizationId()
        {
            var orgIdClaim = User.FindFirst("OrganizationId")?.Value;
            if (int.TryParse(orgIdClaim, out var orgId))
            {
                return orgId;
            }
            return null;
        }

        private int GetRequiredOrganizationId()
        {
            var organizationId = GetOrganizationId();
            if (!organizationId.HasValue)
            {
                throw new ForbiddenException("ADMIN_ORG must belong to an organization");
            }

            return organizationId.Value;
        }

        private async Task EnsureUserInSameOrganizationAsync(int targetUserId, int organizationId)
        {
            var targetUser = await _userService.GetByIdAsync(targetUserId);
            if (targetUser.OrganizationId != organizationId)
            {
                throw new ForbiddenException("You can only manage users from your organization");
            }
        }

        [HttpGet]
        public async Task<ActionResult<UserListResponse>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var organizationId = GetRequiredOrganizationId();
                var result = await _userService.GetAllAsync(organizationId, page, pageSize);
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

        [HttpGet("search")]
        public async Task<ActionResult<UserListResponse>> Search([FromQuery] string? searchTerm, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var organizationId = GetRequiredOrganizationId();
                var result = await _userService.SearchAsync(searchTerm, organizationId, page, pageSize);
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

        [HttpGet("no-process")]
        [Authorize(Roles = UserRoles.ADMIN_ORG)]
        public async Task<ActionResult<UserListResponse>> GetUsersWithNoProcess([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var organizationId = GetRequiredOrganizationId();
                var result = await _userService.GetUsersWithNoProcessAsync(organizationId, page, pageSize);
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

        [HttpGet("{id}")]
        public async Task<ActionResult<UserResponse>> GetById(int id)
        {
            try
            {
                var organizationId = GetRequiredOrganizationId();
                await EnsureUserInSameOrganizationAsync(id, organizationId);
                var user = await _userService.GetByIdAsync(id);
                return Ok(user);
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
        [Authorize(Roles = UserRoles.ADMIN_ORG)]
        public async Task<ActionResult<int>> Create([FromBody] CreateUserRequest request)
        {
            try
            {
                var organizationId = GetRequiredOrganizationId();
                var userId = GetUserId();
                request.OrganizationId = organizationId;
                request.Role = request.Role?.Trim() ?? UserRoles.UTILISATEUR;

                var id = await _userService.CreateAsync(request, userId);
                return CreatedAtAction(nameof(GetById), new { id }, id);
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

        [HttpPut("{id}")]
        [Authorize(Roles = UserRoles.ADMIN_ORG)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var organizationId = GetRequiredOrganizationId();
                await EnsureUserInSameOrganizationAsync(id, organizationId);
                var userId = GetUserId();
                var result = await _userService.UpdateAsync(id, request, userId);
                return result ? NoContent() : BadRequest(new { message = "Failed to update user" });
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

        [HttpPatch("{id}/toggle-status")]
        [Authorize(Roles = UserRoles.ADMIN_ORG)]
        public async Task<IActionResult> ToggleStatus(int id, [FromBody] ToggleUserStatusRequest request)
        {
            try
            {
                var organizationId = GetRequiredOrganizationId();
                await EnsureUserInSameOrganizationAsync(id, organizationId);
                var userId = GetUserId();
                var result = await _userService.ToggleStatusAsync(id, request.IsActive, userId);
                return result ? NoContent() : BadRequest(new { message = "Failed to update user status" });
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

        [HttpPatch("{id}/change-role")]
        [Authorize(Roles = UserRoles.ADMIN_ORG)]
        public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeUserRoleRequest request)
        {
            try
            {
                var organizationId = GetRequiredOrganizationId();
                await EnsureUserInSameOrganizationAsync(id, organizationId);
                var userId = GetUserId();

                if (request.Role == UserRoles.SUPER_ADMIN)
                {
                    throw new ForbiddenException("ADMIN_ORG cannot assign SUPER_ADMIN role");
                }

                var result = await _userService.ChangeRoleAsync(id, request, userId);
                return result ? NoContent() : BadRequest(new { message = "Failed to change user role" });
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

        [HttpDelete("{id}")]
        [Authorize(Roles = UserRoles.ADMIN_ORG)]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var organizationId = GetRequiredOrganizationId();
                await EnsureUserInSameOrganizationAsync(id, organizationId);
                var userId = GetUserId();
                var result = await _userService.DeleteAsync(id, userId);
                return result ? NoContent() : BadRequest(new { message = "Failed to delete user" });
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

        [HttpDelete("{id}/permanent")]
        [Authorize(Roles = UserRoles.ADMIN_ORG)]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            try
            {
                var organizationId = GetRequiredOrganizationId();
                await EnsureUserInSameOrganizationAsync(id, organizationId);
                var result = await _userService.HardDeleteAsync(id);
                return result ? NoContent() : BadRequest(new { message = "Failed to permanently delete user" });
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23503")
            {
                return BadRequest(new { message = "Impossible de supprimer définitivement cet utilisateur car il est lié à d'autres données (documents, donnees associees, etc.). Veuillez plutôt le désactiver." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
