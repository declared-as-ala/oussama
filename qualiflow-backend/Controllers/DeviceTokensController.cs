using DocApi.DTOs.Notifications;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using DocApi.Common;
using Dapper;
using DocApi.Infrastructure;

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

        [HttpGet("list-devices")]
        [AllowAnonymous]
        public async Task<ActionResult> ListDevices([FromServices] IDbConnectionFactory connectionFactory)
        {
            using var connection = connectionFactory.CreateConnection();
            var devices = await connection.QueryAsync<object>("SELECT * FROM UserDevices ORDER BY LastSeenAt DESC;");
            return Ok(devices);
        }

        [HttpGet("test-push-user")]
        [AllowAnonymous]
        public async Task<ActionResult> TestPushUser([FromQuery] int userId, [FromServices] IPushNotificationService pushNotificationService)
        {
            if (userId <= 0)
            {
                return BadRequest("Veuillez fournir un userId valide.");
            }

            var notification = new DocApi.Domain.Entities.Notification
            {
                UserId = userId,
                Type = NotificationConstants.TypeSystemAlert,
                Category = NotificationConstants.CategoryInfo,
                Title = "Test FCM QualiFlow",
                Message = "Félicitations ! Vos notifications push FCM fonctionnent parfaitement sur votre Infinix.",
                RedirectUrl = "/notifications",
                CreatedAt = System.DateTime.UtcNow
            };

            var result = await pushNotificationService.SendAsync(notification);
            return Ok(new
            {
                success = result.IsSent,
                channel = result.Channel,
                providerId = result.ExternalProviderId,
                message = result.IsSent ? "Notification envoyee avec succes !" : "Echec de l'envoi de la notification. Verifiez que l'appareil a enregistre son token et que Firebase est active."
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
