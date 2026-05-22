using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.Notifications;
using DocApi.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/notifications/web-push")]
    [Authorize]
    public sealed class NotificationWebPushController : ControllerBase
    {
        private readonly IWebPushSubscriptionRepository _webPushSubscriptionRepository;

        public NotificationWebPushController(IWebPushSubscriptionRepository webPushSubscriptionRepository)
        {
            _webPushSubscriptionRepository = webPushSubscriptionRepository;
        }

        [HttpPost("subscribe")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<object>> Subscribe([FromBody] RegisterWebPushSubscriptionRequest request)
        {
            var userContext = GetUserContext();

            var id = await _webPushSubscriptionRepository.UpsertAsync(new WebPushSubscription
            {
                UserId = userContext.UserId,
                OrganizationId = userContext.OrganizationId,
                Endpoint = request.Endpoint.Trim(),
                P256dh = request.P256dh.Trim(),
                Auth = request.Auth.Trim(),
                UserAgent = Request.Headers.UserAgent.ToString()
            });

            return Ok(new { id, success = true });
        }

        [HttpPost("unsubscribe")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<object>> Unsubscribe([FromBody] UnregisterWebPushSubscriptionRequest request)
        {
            var userContext = GetUserContext();
            var removed = await _webPushSubscriptionRepository.DeactivateAsync(userContext.UserId, request.Endpoint.Trim());
            return Ok(new { removed });
        }

        [HttpGet("my-subscriptions")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<IReadOnlyList<WebPushSubscriptionResponse>>> GetMySubscriptions()
        {
            var userContext = GetUserContext();
            var rows = await _webPushSubscriptionRepository.GetActiveByUserAsync(userContext.UserId);

            var payload = rows.Select(row => new WebPushSubscriptionResponse
            {
                Id = row.Id,
                Endpoint = row.Endpoint,
                IsActive = row.IsActive,
                CreatedAt = row.CreatedAt,
                UpdatedAt = row.UpdatedAt,
                LastUsedAt = row.LastUsedAt
            }).ToList();

            return Ok(payload);
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
