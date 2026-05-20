using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.Domain.Enums;
using DocApi.DTOs.Notifications;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;

namespace DocApi.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly INotificationGeneratorService _notificationGeneratorService;
        private readonly INotificationRecipientService _notificationRecipientService;
        private readonly IDocumentNotificationRepository _documentNotificationRepository;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IUserRepository _userRepository;
        private readonly SignalRNotificationService _signalRNotificationService;

        public NotificationService(
            INotificationRepository notificationRepository,
            INotificationGeneratorService notificationGeneratorService,
            INotificationRecipientService notificationRecipientService,
            IDocumentNotificationRepository documentNotificationRepository,
            IPushNotificationService pushNotificationService,
            IUserRepository userRepository,
            SignalRNotificationService signalRNotificationService)
        {
            _notificationRepository = notificationRepository;
            _notificationGeneratorService = notificationGeneratorService;
            _notificationRecipientService = notificationRecipientService;
            _documentNotificationRepository = documentNotificationRepository;
            _pushNotificationService = pushNotificationService;
            _userRepository = userRepository;
            _signalRNotificationService = signalRNotificationService;
        }

        public async Task<PagedNotificationResponse> GetNotificationsAsync(GetNotificationsQueryRequest query, UserContext userContext)
        {
            EnsureCanRead(userContext);
            ValidateQueryFilters(query);
            await _notificationGeneratorService.GenerateAutomaticAlertsForUserAsync(userContext);

            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
            var organizationScope = ResolveOrganizationScope(userContext, query.OrganizationId);

            var items = await _notificationRepository.SearchAsync(
                pageNumber,
                pageSize,
                userContext.UserId,
                organizationScope,
                NormalizeSearch(query.Search),
                query.IsRead,
                NormalizeUpper(query.Category),
                NormalizeUpper(query.Priority),
                NormalizeUpper(query.Type),
                query.FromDate,
                query.ToDate,
                includeArchived: false);

            var total = await _notificationRepository.CountSearchAsync(
                userContext.UserId,
                organizationScope,
                NormalizeSearch(query.Search),
                query.IsRead,
                NormalizeUpper(query.Category),
                NormalizeUpper(query.Priority),
                NormalizeUpper(query.Type),
                query.FromDate,
                query.ToDate,
                includeArchived: false);

            return new PagedNotificationResponse
            {
                Total = total,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Items = items.Select(MapToListItem).ToList()
            };
        }

        public async Task<NotificationResponse> GetByIdAsync(int id, UserContext userContext)
        {
            EnsureCanRead(userContext);
            var notification = await GetOwnedNotificationOrThrowAsync(id, userContext);
            return MapToResponse(notification);
        }

        public async Task<int> GetUnreadCountAsync(UserContext userContext)
        {
            EnsureCanRead(userContext);
            await _notificationGeneratorService.GenerateAutomaticAlertsForUserAsync(userContext);
            return await _notificationRepository.GetUnreadCountAsync(userContext.UserId, userContext.OrganizationId);
        }

        public async Task<NotificationStatisticsResponse> GetStatisticsAsync(UserContext userContext)
        {
            EnsureCanRead(userContext);
            await _notificationGeneratorService.GenerateAutomaticAlertsForUserAsync(userContext);

            var notifications = (await _notificationRepository.GetForUserAsync(
                userContext.UserId,
                userContext.OrganizationId,
                includeArchived: true)).ToList();

            return new NotificationStatisticsResponse
            {
                Total = notifications.Count,
                Unread = notifications.Count(item => !item.IsRead && !item.IsArchived),
                Read = notifications.Count(item => item.IsRead && !item.IsArchived),
                Archived = notifications.Count(item => item.IsArchived),
                Critical = notifications.Count(item => item.Priority == NotificationConstants.PriorityCritical && !item.IsArchived),
                High = notifications.Count(item => item.Priority == NotificationConstants.PriorityHigh && !item.IsArchived),
                ByCategory = notifications
                    .Where(item => !item.IsArchived)
                    .GroupBy(item => item.Category)
                    .ToDictionary(group => group.Key, group => group.Count()),
                ByType = notifications
                    .Where(item => !item.IsArchived)
                    .GroupBy(item => item.Type)
                    .ToDictionary(group => group.Key, group => group.Count())
            };
        }

        public async Task<NotificationResponse> MarkAsReadAsync(int id, UserContext userContext)
        {
            EnsureCanRead(userContext);
            var notification = await GetOwnedNotificationOrThrowAsync(id, userContext);

            if (!notification.IsRead)
            {
                await _notificationRepository.MarkAsReadAsync(id, userContext.UserId, DateTime.UtcNow);
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }

            return MapToResponse(notification);
        }

        public async Task<int> MarkAllAsReadAsync(UserContext userContext)
        {
            EnsureCanRead(userContext);
            return await _notificationRepository.MarkAllAsReadAsync(userContext.UserId, userContext.OrganizationId, DateTime.UtcNow);
        }

        public async Task<NotificationResponse> ArchiveAsync(int id, UserContext userContext)
        {
            EnsureCanRead(userContext);
            var notification = await GetOwnedNotificationOrThrowAsync(id, userContext);
            await _notificationRepository.ArchiveAsync(id, userContext.UserId);
            notification.IsArchived = true;
            notification.IsRead = true;
            notification.ReadAt ??= DateTime.UtcNow;
            return MapToResponse(notification);
        }

        public async Task<bool> DeleteAsync(int id, UserContext userContext)
        {
            EnsureCanRead(userContext);
            await GetOwnedNotificationOrThrowAsync(id, userContext);
            return await _notificationRepository.DeleteAsync(id, userContext.UserId);
        }

        public async Task<IReadOnlyList<NotificationResponse>> CreateAsync(CreateNotificationRequest request, UserContext userContext)
        {
            EnsureCanRead(userContext);

            if (!userContext.IsSuperAdmin &&
                !string.Equals(userContext.Role, UserRoles.ADMIN_ORG, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(userContext.Role, UserRoles.RESPONSABLE_QUALITE, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("Vous n'avez pas les droits d'envoi de notification.");
            }

            var title = request.Title?.Trim();
            var message = request.Message?.Trim();
            var type = request.Type?.Trim().ToUpperInvariant();
            var category = NormalizeUpper(request.Category) ?? NotificationConstants.CategoryInfo;
            var priority = NormalizeUpper(request.Priority) ?? NotificationConstants.PriorityMedium;
            var redirectUrl = NormalizeRoute(request.RedirectUrl ?? request.ActionUrl ?? "/notifications");
            var sourceModule = string.IsNullOrWhiteSpace(request.SourceModule) ? null : request.SourceModule.Trim();
            var referenceType = NormalizeUpper(request.ReferenceType);
            var referenceId = request.ReferenceId?.ToString();

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ServiceException("Le titre de notification est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ServiceException("Le message de notification est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ServiceException("Le type de notification est obligatoire.");
            }

            ValidateValueInSet(category, NotificationConstants.AllowedCategories, "Categorie");
            ValidateValueInSet(priority, NotificationConstants.AllowedPriorities, "Priorite");
            ValidateValueInSet(type, NotificationConstants.AllowedTypes, "Type");

            var targetOrganizationId = ResolveOrganizationScope(userContext, request.OrganizationId);
            var recipientUserIds = new HashSet<int>();

            if (request.TargetUserId.HasValue && request.TargetUserId.Value > 0)
            {
                recipientUserIds.Add(request.TargetUserId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.TargetRole))
            {
                if (!targetOrganizationId.HasValue)
                {
                    throw new ServiceException("OrganizationId est obligatoire pour cibler un role.");
                }

                var usersByRole = await _userRepository.GetActiveByOrganizationAndRolesAsync(
                    targetOrganizationId.Value,
                    new[] { request.TargetRole.Trim() });

                foreach (var user in usersByRole)
                {
                    recipientUserIds.Add(user.Id);
                }
            }

            if (recipientUserIds.Count == 0)
            {
                throw new ServiceException("Aucun destinataire valide (TargetUserId ou TargetRole).");
            }

            var created = new List<NotificationResponse>();
            var now = DateTime.UtcNow;

            foreach (var recipientUserId in recipientUserIds)
            {
                var notification = new Notification
                {
                    OrganizationId = targetOrganizationId,
                    UserId = recipientUserId,
                    SenderId = userContext.UserId,
                    Type = type!,
                    Category = category,
                    Title = title!,
                    Message = message!,
                    Priority = priority,
                    IsRead = false,
                    IsArchived = false,
                    IsPushSent = false,
                    Channel = "INAPP",
                    SourceModule = sourceModule,
                    ReferenceType = referenceType,
                    ReferenceId = referenceId,
                    EntityType = sourceModule,
                    EntityId = request.ReferenceId,
                    RedirectUrl = redirectUrl,
                    ActionUrl = redirectUrl,
                    TargetRole = string.IsNullOrWhiteSpace(request.TargetRole) ? null : request.TargetRole.Trim().ToUpperInvariant(),
                    CreatedAt = now
                };

                notification.Id = await _notificationRepository.CreateAsync(notification);
                if (notification.Id <= 0)
                {
                    continue;
                }

                var unreadCount = await _notificationRepository.GetUnreadCountAsync(notification.UserId, notification.OrganizationId);
                await _signalRNotificationService.SendToUserAsync(notification, unreadCount);

                var pushDispatch = await _pushNotificationService.SendAsync(notification);
                if (pushDispatch.IsSent)
                {
                    notification.IsPushSent = true;
                    notification.Channel = pushDispatch.Channel;
                    notification.ExternalProviderId = pushDispatch.ExternalProviderId;
                    await _notificationRepository.MarkPushSentAsync(notification.Id, pushDispatch.ExternalProviderId, pushDispatch.Channel);
                }

                created.Add(MapToResponse(notification));
            }

            return created;
        }

        public async Task<IReadOnlyList<NotificationRecipientResponse>> GetRecipientsAsync(NotificationRecipientsQueryRequest query, UserContext userContext)
        {
            EnsureCanRead(userContext);

            if (!userContext.IsSuperAdmin && !userContext.OrganizationId.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable pour l'utilisateur.");
            }

            var organizationId = userContext.OrganizationId;
            if (!organizationId.HasValue)
            {
                throw new ServiceException("OrganizationId est obligatoire pour recuperer les destinataires.");
            }

            var eventType = WorkflowEventMapper.ParseEventType(query.EventType);
            return await _notificationRecipientService.GetRecipientsAsync(
                organizationId.Value,
                eventType,
                query.DocumentId);
        }

        public async Task<NotificationLogResponse> LogDocumentNotificationAsync(NotificationLogRequest request, int organizationId, int? triggeredByUserId)
        {
            var normalizedEventType = WorkflowEventMapper.ParseEventType(request.EventType).ToString();
            var normalizedRoleType = string.IsNullOrWhiteSpace(request.RecipientRoleType)
                ? RoleType.Employee.ToString()
                : request.RecipientRoleType.Trim();

            var now = DateTime.UtcNow;
            var entity = new DocumentNotification
            {
                OrganizationId = organizationId,
                DocumentId = request.DocumentId,
                DocumentVersionId = request.DocumentVersionId,
                EventType = normalizedEventType,
                RecipientUserId = request.RecipientUserId,
                RecipientRole = normalizedRoleType,
                Channel = string.IsNullOrWhiteSpace(request.Channel) ? "EMAIL" : request.Channel.Trim().ToUpperInvariant(),
                Subject = request.Subject.Trim(),
                Message = request.Message.Trim(),
                DeliveryStatus = string.IsNullOrWhiteSpace(request.DeliveryStatus) ? "SENT" : request.DeliveryStatus.Trim().ToUpperInvariant(),
                ExternalMessageId = string.IsNullOrWhiteSpace(request.ExternalMessageId) ? null : request.ExternalMessageId.Trim(),
                PayloadJson = string.IsNullOrWhiteSpace(request.PayloadJson) ? null : request.PayloadJson.Trim(),
                SentAt = request.SentAt ?? now,
                CreatedAt = now
            };

            var id = await _documentNotificationRepository.CreateAsync(entity);

            if (request.RecipientUserId.HasValue && request.RecipientUserId.Value > 0)
            {
                var userNotification = new Notification
                {
                    OrganizationId = organizationId,
                    UserId = request.RecipientUserId.Value,
                    SenderId = triggeredByUserId,
                    Type = normalizedEventType,
                    Category = NotificationConstants.CategoryInfo,
                    Title = request.Subject.Trim(),
                    Message = request.Message.Trim(),
                    Priority = NotificationConstants.PriorityMedium,
                    IsRead = false,
                    IsArchived = false,
                    IsPushSent = false,
                    Channel = "INAPP",
                    DocumentId = request.DocumentId,
                    ReferenceType = "DOCUMENT",
                    ReferenceId = request.DocumentId.ToString(),
                    SourceModule = "DOCUMENT",
                    RedirectUrl = $"/documents/{request.DocumentId}",
                    ActionUrl = $"/documents/{request.DocumentId}",
                    CreatedAt = now
                };

                userNotification.Id = await _notificationRepository.CreateAsync(userNotification);

                if (userNotification.Id > 0)
                {
                    var unreadCount = await _notificationRepository.GetUnreadCountAsync(
                        userNotification.UserId,
                        userNotification.OrganizationId);
                    await _signalRNotificationService.SendToUserAsync(userNotification, unreadCount);

                    var pushDispatch = await _pushNotificationService.SendAsync(userNotification);
                    if (pushDispatch.IsSent)
                    {
                        userNotification.IsPushSent = true;
                        userNotification.Channel = pushDispatch.Channel;
                        userNotification.ExternalProviderId = pushDispatch.ExternalProviderId;
                        await _notificationRepository.MarkPushSentAsync(
                            userNotification.Id,
                            pushDispatch.ExternalProviderId,
                            pushDispatch.Channel);
                    }
                }
            }

            return new NotificationLogResponse
            {
                Id = id,
                OrganizationId = organizationId,
                DocumentId = entity.DocumentId,
                DocumentVersionId = entity.DocumentVersionId,
                EventType = entity.EventType,
                RecipientUserId = entity.RecipientUserId,
                RecipientRoleType = entity.RecipientRole,
                Channel = entity.Channel,
                Subject = entity.Subject,
                Message = entity.Message,
                DeliveryStatus = entity.DeliveryStatus,
                ExternalMessageId = entity.ExternalMessageId,
                SentAt = entity.SentAt,
                CreatedAt = entity.CreatedAt
            };
        }

        private static void EnsureCanRead(UserContext userContext)
        {
            if (!userContext.CanReadNotifications || !NotificationConstants.AllowedNotificationRoles.Contains(userContext.Role))
            {
                throw new ForbiddenException("Vous n'avez pas les droits de lecture sur les notifications.");
            }

            if (!userContext.IsSuperAdmin && !userContext.OrganizationId.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable pour l'utilisateur.");
            }
        }

        private static int? ResolveOrganizationScope(UserContext userContext, int? requestedOrganizationId)
        {
            if (!requestedOrganizationId.HasValue)
            {
                return userContext.OrganizationId;
            }

            if (!userContext.IsSuperAdmin && userContext.OrganizationId != requestedOrganizationId)
            {
                throw new ForbiddenException("Acces refuse a l'organisation demandee.");
            }

            return requestedOrganizationId;
        }

        private async Task<Notification> GetOwnedNotificationOrThrowAsync(int notificationId, UserContext userContext)
        {
            var notification = await _notificationRepository.GetByIdAsync(notificationId);
            if (notification == null)
            {
                throw new NotFoundException("Notification introuvable.");
            }

            if (notification.UserId != userContext.UserId)
            {
                throw new ForbiddenException("Vous ne pouvez pas acceder a une notification d'un autre utilisateur.");
            }

            if (!userContext.IsSuperAdmin &&
                notification.OrganizationId.HasValue &&
                notification.OrganizationId != userContext.OrganizationId)
            {
                throw new ForbiddenException("Vous ne pouvez pas acceder a une notification d'une autre organisation.");
            }

            return notification;
        }

        private static NotificationListItemResponse MapToListItem(Notification notification)
        {
            return new NotificationListItemResponse
            {
                Id = notification.Id,
                SenderId = notification.SenderId,
                Type = notification.Type,
                Category = notification.Category,
                Title = notification.Title,
                Message = notification.Message,
                Priority = notification.Priority,
                IsRead = notification.IsRead,
                IsPushSent = notification.IsPushSent,
                Channel = notification.Channel,
                ExternalProviderId = notification.ExternalProviderId,
                IsArchived = notification.IsArchived,
                DocumentId = notification.DocumentId,
                EntityType = notification.EntityType,
                EntityId = notification.EntityId,
                SourceModule = notification.SourceModule,
                RedirectUrl = notification.RedirectUrl,
                ExpiresAt = notification.ExpiresAt,
                ActionUrl = notification.ActionUrl,
                ReferenceType = notification.ReferenceType,
                ReferenceId = notification.ReferenceId,
                CreatedAt = notification.CreatedAt
            };
        }

        private static NotificationResponse MapToResponse(Notification notification)
        {
            return new NotificationResponse
            {
                Id = notification.Id,
                OrganizationId = notification.OrganizationId,
                UserId = notification.UserId,
                SenderId = notification.SenderId,
                Type = notification.Type,
                Category = notification.Category,
                Title = notification.Title,
                Message = notification.Message,
                Priority = notification.Priority,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                IsPushSent = notification.IsPushSent,
                Channel = notification.Channel,
                ExternalProviderId = notification.ExternalProviderId,
                IsArchived = notification.IsArchived,
                DocumentId = notification.DocumentId,
                EntityType = notification.EntityType,
                EntityId = notification.EntityId,
                SourceModule = notification.SourceModule,
                RedirectUrl = notification.RedirectUrl,
                ExpiresAt = notification.ExpiresAt,
                ReferenceType = notification.ReferenceType,
                ReferenceId = notification.ReferenceId,
                ActionUrl = notification.ActionUrl,
                CreatedAt = notification.CreatedAt
            };
        }

        private static string? NormalizeSearch(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static string? NormalizeUpper(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim().ToUpperInvariant();
        }

        private static string NormalizeRoute(string route)
        {
            var normalized = string.IsNullOrWhiteSpace(route) ? "/notifications" : route.Trim();
            return normalized.StartsWith('/') ? normalized : $"/{normalized}";
        }

        private static void ValidateQueryFilters(GetNotificationsQueryRequest query)
        {
            if (query.PageNumber <= 0)
            {
                throw new ServiceException("Le numero de page doit etre superieur a zero.");
            }

            if (query.PageSize <= 0)
            {
                throw new ServiceException("La taille de page doit etre superieure a zero.");
            }

            if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate.Value > query.ToDate.Value)
            {
                throw new ServiceException("La date de debut ne peut pas etre superieure a la date de fin.");
            }

            ValidateValueInSet(NormalizeUpper(query.Category), NotificationConstants.AllowedCategories, "Categorie");
            ValidateValueInSet(NormalizeUpper(query.Priority), NotificationConstants.AllowedPriorities, "Priorite");
            ValidateValueInSet(NormalizeUpper(query.Type), NotificationConstants.AllowedTypes, "Type");
        }

        private static void ValidateValueInSet(string? value, ISet<string> allowedValues, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!allowedValues.Contains(value))
            {
                throw new ServiceException($"{label} de notification invalide: {value}.");
            }
        }
    }
}
