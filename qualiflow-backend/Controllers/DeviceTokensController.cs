using DocApi.DTOs.Notifications;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using DocApi.Common;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/device-tokens")]
    [Authorize]
    public class DeviceTokensController : ControllerBase
    {
        private readonly IDeviceTokenService _deviceTokenService;

        public DeviceTokensController(IDeviceTokenService deviceTokenService)
        {
            _deviceTokenService = deviceTokenService;
        }

        [HttpPost("register")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<object>> Register([FromBody] RegisterDeviceTokenRequest request)
        {
            var id = await _deviceTokenService.RegisterAsync(GetUserContext(), request);
            return Ok(new
            {
                success = true,
                deviceId = id
            });
        }

        [HttpPost("unregister")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<object>> Unregister([FromBody] UnregisterDeviceTokenRequest request)
        {
            var removed = await _deviceTokenService.UnregisterAsync(GetUserContext(), request);
            return Ok(new
            {
                success = true,
                removed
            });
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
