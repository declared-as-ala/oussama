using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.Notifications;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly IOneSignalService _oneSignalService;
        private readonly INotificationRepository _notificationRepository;
        private readonly INotificationPreferenceRepository _notificationPreferenceRepository;

        private static readonly string[] ManageableNotificationTypes =
        {
            NotificationConstants.TypeDocumentApprovalRequired,
            NotificationConstants.TypeDocumentExpired,
            NotificationConstants.TypeDocumentNewVersion,
            NotificationConstants.TypeNonConformityCreated,
            NotificationConstants.TypeCorrectiveActionAssigned,
            NotificationConstants.TypeIndicatorAlert,
            NotificationConstants.TypeSystemAlert
        };

        public NotificationsController(
            INotificationService notificationService,
            IOneSignalService oneSignalService,
            INotificationRepository notificationRepository,
            INotificationPreferenceRepository notificationPreferenceRepository)
        {
            _notificationService = notificationService;
            _oneSignalService = oneSignalService;
            _notificationRepository = notificationRepository;
            _notificationPreferenceRepository = notificationPreferenceRepository;
        }

        [HttpGet]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<PagedNotificationResponse>> GetNotifications([FromQuery] GetNotificationsQueryRequest query)
        {
            try
            {
                var result = await _notificationService.GetNotificationsAsync(query, GetUserContext());
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

        [HttpGet("unread")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<PagedNotificationResponse>> GetUnreadNotifications([FromQuery] GetNotificationsQueryRequest query)
        {
            try
            {
                query.IsRead = false;
                var result = await _notificationService.GetNotificationsAsync(query, GetUserContext());
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
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<NotificationResponse>> GetById(int id)
        {
            try
            {
                var result = await _notificationService.GetByIdAsync(id, GetUserContext());
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

        [HttpGet("unread-count")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<object>> GetUnreadCount()
        {
            try
            {
                var unread = await _notificationService.GetUnreadCountAsync(GetUserContext());
                return Ok(new { unreadCount = unread });
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
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<NotificationStatisticsResponse>> GetStatistics()
        {
            try
            {
                var result = await _notificationService.GetStatisticsAsync(GetUserContext());
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

        [HttpGet("preferences")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<IReadOnlyList<NotificationPreferenceResponse>>> GetPreferences()
        {
            try
            {
                var userContext = GetUserContext();
                var stored = (await _notificationPreferenceRepository.GetByUserIdAsync(userContext.UserId))
                    .ToDictionary(item => item.NotificationType, StringComparer.OrdinalIgnoreCase);

                var result = ManageableNotificationTypes
                    .Select(type =>
                    {
                        if (stored.TryGetValue(type, out var preference))
                        {
                            return new NotificationPreferenceResponse
                            {
                                NotificationType = type,
                                InAppEnabled = preference.InAppEnabled,
                                EmailEnabled = preference.EmailEnabled
                            };
                        }

                        return new NotificationPreferenceResponse
                        {
                            NotificationType = type,
                            InAppEnabled = true,
                            EmailEnabled = false
                        };
                    })
                    .ToList();

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

        [HttpPut("preferences")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<IReadOnlyList<NotificationPreferenceResponse>>> UpdatePreferences([FromBody] UpdateNotificationPreferencesRequest request)
        {
            try
            {
                if (request?.Items == null || request.Items.Count == 0)
                {
                    throw new ServiceException("Aucune preference de notification fournie.");
                }

                var userContext = GetUserContext();
                var requestedTypes = new HashSet<string>(ManageableNotificationTypes, StringComparer.OrdinalIgnoreCase);

                foreach (var item in request.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.NotificationType) || !requestedTypes.Contains(item.NotificationType))
                    {
                        throw new ServiceException($"Type de notification invalide: {item.NotificationType}");
                    }

                    await _notificationPreferenceRepository.UpsertAsync(new NotificationPreference
                    {
                        UserId = userContext.UserId,
                        NotificationType = item.NotificationType.Trim(),
                        InAppEnabled = item.InAppEnabled,
                        EmailEnabled = item.EmailEnabled,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                return await GetPreferences();
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

        [HttpPatch("{id:int}/mark-read")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<NotificationResponse>> MarkRead(int id, [FromBody] MarkNotificationReadRequest? _request = null)
        {
            try
            {
                var result = await _notificationService.MarkAsReadAsync(id, GetUserContext());
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

        [HttpPut("{id:int}/read")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public Task<ActionResult<NotificationResponse>> MarkReadPut(int id, [FromBody] MarkNotificationReadRequest? request = null)
        {
            return MarkRead(id, request);
        }

        [HttpPatch("mark-all-read")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<object>> MarkAllRead([FromBody] MarkAllNotificationsReadRequest? _request = null)
        {
            try
            {
                var updated = await _notificationService.MarkAllAsReadAsync(GetUserContext());
                return Ok(new { updatedCount = updated });
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

        [HttpPost]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> Create([FromBody] CreateNotificationRequest request)
        {
            try
            {
                var result = await _notificationService.CreateAsync(request, GetUserContext());
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

        [HttpPut("mark-all-read")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public Task<ActionResult<object>> MarkAllReadPut([FromBody] MarkAllNotificationsReadRequest? request = null)
        {
            return MarkAllRead(request);
        }

        [HttpPatch("{id:int}/archive")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<ActionResult<NotificationResponse>> Archive(int id, [FromBody] ArchiveNotificationRequest? _request = null)
        {
            try
            {
                var result = await _notificationService.ArchiveAsync(id, GetUserContext());
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
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,UTILISATEUR")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _notificationService.DeleteAsync(id, GetUserContext());
                if (!deleted)
                {
                    return NotFound(new { message = "Notification introuvable." });
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

        [HttpGet("recipients")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<IReadOnlyList<NotificationRecipientResponse>>> GetRecipients([FromQuery] NotificationRecipientsQueryRequest query)
        {
            try
            {
                var result = await _notificationService.GetRecipientsAsync(query, GetUserContext());
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

        [HttpPost("send-test")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE")]
        public async Task<ActionResult<object>> SendTest([FromBody] SendOneSignalNotificationRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest(new { message = "Title et Message sont obligatoires." });
                }

                var redirectUrl = string.IsNullOrWhiteSpace(request.RedirectUrl)
                    ? (request.DocumentId.HasValue ? $"/documents/{request.DocumentId.Value}" : "/notifications")
                    : request.RedirectUrl.Trim();

                var data = new Dictionary<string, string>
                {
                    ["redirectUrl"] = redirectUrl,
                    ["type"] = string.IsNullOrWhiteSpace(request.Type) ? "ALERT" : request.Type.Trim().ToUpperInvariant(),
                    ["documentId"] = request.DocumentId?.ToString() ?? string.Empty
                };

                if (!string.IsNullOrWhiteSpace(request.ExternalId) || (request.ExternalIds?.Count ?? 0) > 0)
                {
                    var externalIds = new List<string>();
                    if (!string.IsNullOrWhiteSpace(request.ExternalId))
                    {
                        externalIds.Add(request.ExternalId.Trim());
                    }

                    if (request.ExternalIds != null && request.ExternalIds.Count > 0)
                    {
                        externalIds.AddRange(request.ExternalIds);
                    }

                    var sendResult = await _oneSignalService.SendToExternalIdsAsync(
                        externalIds,
                        request.Title.Trim(),
                        request.Message.Trim(),
                        data);

                    if (!sendResult.IsSuccess)
                    {
                        return BadRequest(new { message = sendResult.Error ?? "Echec envoi OneSignal." });
                    }

                    foreach (var externalId in externalIds)
                    {
                        if (!int.TryParse(externalId, out var userId) || userId <= 0)
                        {
                            continue;
                        }

                        await _notificationRepository.CreateAsync(new Notification
                        {
                            OrganizationId = request.OrganizationId,
                            UserId = userId,
                            SenderId = GetUserContext().UserId,
                            Type = string.IsNullOrWhiteSpace(request.Type) ? "ALERT" : request.Type.Trim().ToUpperInvariant(),
                            Category = NotificationConstants.CategoryInfo,
                            Title = request.Title.Trim(),
                            Message = request.Message.Trim(),
                            Priority = NotificationConstants.PriorityMedium,
                            IsRead = false,
                            IsPushSent = true,
                            IsArchived = false,
                            Channel = "PUSH",
                            ExternalProviderId = sendResult.NotificationId,
                            DocumentId = request.DocumentId,
                            ActionUrl = redirectUrl,
                            RedirectUrl = redirectUrl,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    return Ok(new
                    {
                        success = true,
                        provider = "OneSignal",
                        externalProviderId = sendResult.NotificationId
                    });
                }

                var tags = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(request.Role))
                {
                    tags["role"] = request.Role.Trim();
                }

                if (request.OrganizationId.HasValue)
                {
                    tags["organizationId"] = request.OrganizationId.Value.ToString();
                }

                var tagsResult = await _oneSignalService.SendByTagsAsync(
                    tags,
                    request.Title.Trim(),
                    request.Message.Trim(),
                    data);

                if (!tagsResult.IsSuccess)
                {
                    return BadRequest(new { message = tagsResult.Error ?? "Echec envoi OneSignal." });
                }

                return Ok(new
                {
                    success = true,
                    provider = "OneSignal",
                    externalProviderId = tagsResult.NotificationId,
                    tags
                });
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
