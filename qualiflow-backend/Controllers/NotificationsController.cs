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
        private readonly IPushNotificationService _pushNotificationService;
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
            IPushNotificationService pushNotificationService,
            INotificationRepository notificationRepository,
            INotificationPreferenceRepository notificationPreferenceRepository)
        {
            _notificationService = notificationService;
            _pushNotificationService = pushNotificationService;
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
        public async Task<ActionResult<object>> SendTest([FromBody] SendPushNotificationRequest request)
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

                var userIds = new List<int>();
                if (request.UserId.HasValue && request.UserId.Value > 0)
                {
                    userIds.Add(request.UserId.Value);
                }

                if (request.UserIds != null && request.UserIds.Count > 0)
                {
                    userIds.AddRange(request.UserIds.Where(id => id > 0));
                }

                if (!string.IsNullOrWhiteSpace(request.ExternalId)
                    && int.TryParse(request.ExternalId, out var legacyUserId)
                    && legacyUserId > 0)
                {
                    userIds.Add(legacyUserId);
                }

                if (request.ExternalIds != null && request.ExternalIds.Count > 0)
                {
                    foreach (var externalId in request.ExternalIds)
                    {
                        if (!int.TryParse(externalId, out var userId) || userId <= 0)
                        {
                            continue;
                        }

                        userIds.Add(userId);
                    }
                }

                var distinctUserIds = userIds.Distinct().ToArray();
                if (distinctUserIds.Length == 0)
                {
                    return BadRequest(new { message = "Au moins un UserId est obligatoire pour envoyer un test FCM." });
                }

                var sender = GetUserContext();
                var sentCount = 0;
                var providerIds = new List<string>();

                foreach (var userId in distinctUserIds)
                {
                    var notification = new Notification
                    {
                        OrganizationId = request.OrganizationId,
                        UserId = userId,
                        SenderId = sender.UserId,
                        Type = string.IsNullOrWhiteSpace(request.Type) ? "ALERT" : request.Type.Trim().ToUpperInvariant(),
                        Category = NotificationConstants.CategoryInfo,
                        Title = request.Title.Trim(),
                        Message = request.Message.Trim(),
                        Priority = NotificationConstants.PriorityMedium,
                        IsRead = false,
                        IsPushSent = false,
                        IsArchived = false,
                        DocumentId = request.DocumentId,
                        ActionUrl = redirectUrl,
                        RedirectUrl = redirectUrl,
                        CreatedAt = DateTime.UtcNow
                    };

                    notification.Id = await _notificationRepository.CreateAsync(notification);
                    var pushDispatch = await _pushNotificationService.SendAsync(notification);

                    if (pushDispatch.IsSent)
                    {
                        sentCount++;
                        notification.IsPushSent = true;
                        notification.Channel = pushDispatch.Channel;
                        notification.ExternalProviderId = pushDispatch.ExternalProviderId;
                        await _notificationRepository.MarkPushSentAsync(
                            notification.Id,
                            pushDispatch.ExternalProviderId,
                            pushDispatch.Channel);

                        if (!string.IsNullOrWhiteSpace(pushDispatch.ExternalProviderId))
                        {
                            providerIds.Add(pushDispatch.ExternalProviderId);
                        }
                    }
                }

                return Ok(new
                {
                    success = sentCount > 0,
                    provider = "FCM",
                    sentCount,
                    requestedUsers = distinctUserIds.Length,
                    externalProviderIds = providerIds
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
