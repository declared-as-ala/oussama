using System;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocApi.Services
{
    public sealed class NotificationConsumerService : INotificationConsumerService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IUserRepository _userRepository;
        private readonly SignalRNotificationService _signalRNotificationService;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ILogger<NotificationConsumerService> _logger;

        public NotificationConsumerService(
            INotificationRepository notificationRepository,
            IUserRepository userRepository,
            SignalRNotificationService signalRNotificationService,
            IPushNotificationService pushNotificationService,
            ILogger<NotificationConsumerService> logger)
        {
            _notificationRepository = notificationRepository;
            _userRepository = userRepository;
            _signalRNotificationService = signalRNotificationService;
            _pushNotificationService = pushNotificationService;
            _logger = logger;
        }

        public async Task HandleAsync(NotificationEventMessage message, CancellationToken cancellationToken = default)
        {
            ValidateMessage(message);

            var user = await _userRepository.GetByIdAsync(message.UserId);
            if (user == null || !user.IsActive)
            {
                throw new ServiceException($"Utilisateur cible introuvable ou inactif: {message.UserId}.");
            }

            var organizationId = ResolveOrganizationId(message, user);
            if (user.OrganizationId.HasValue && user.OrganizationId.Value != organizationId)
            {
                throw new ForbiddenException("Notification refusee: organisation cible invalide.");
            }

            int? senderId = message.SenderId ?? message.TriggeredByUserId;
            if (senderId.HasValue)
            {
                var sender = await _userRepository.GetByIdAsync(senderId.Value);
                if (sender == null)
                {
                    senderId = null;
                }
            }

            var createdAt = message.TriggeredAt == default ? DateTime.UtcNow : DateTime.SpecifyKind(message.TriggeredAt, DateTimeKind.Utc);
            var notification = new Notification
            {
                OrganizationId = organizationId,
                UserId = message.UserId,
                SenderId = senderId,
                Type = NormalizeAndValidate(message.Type, NotificationConstants.AllowedTypes, "Type"),
                Category = NormalizeAndValidate(message.Category, NotificationConstants.AllowedCategories, "Category"),
                Title = message.Title.Trim(),
                Message = message.Message.Trim(),
                Priority = NormalizeAndValidate(message.Priority, NotificationConstants.AllowedPriorities, "Priority", NotificationConstants.PriorityMedium),
                IsRead = false,
                IsPushSent = false,
                Channel = "INAPP",
                DocumentId = message.EntityType?.Trim().Equals("DOCUMENT", StringComparison.OrdinalIgnoreCase) == true
                    ? message.EntityId
                    : null,
                IsArchived = false,
                EntityType = string.IsNullOrWhiteSpace(message.EntityType) ? message.ReferenceType?.Trim() : message.EntityType.Trim(),
                EntityId = message.EntityId,
                RedirectUrl = string.IsNullOrWhiteSpace(message.RedirectUrl) ? message.ActionUrl?.Trim() : message.RedirectUrl.Trim(),
                ExpiresAt = message.ExpiresAt,
                ReferenceType = string.IsNullOrWhiteSpace(message.ReferenceType) ? null : message.ReferenceType.Trim(),
                ReferenceId = string.IsNullOrWhiteSpace(message.ReferenceId) ? null : message.ReferenceId.Trim(),
                ActionUrl = string.IsNullOrWhiteSpace(message.ActionUrl) ? null : message.ActionUrl.Trim(),
                CreatedAt = createdAt
            };

            var alreadyExists = await _notificationRepository.ExistsSimilarInWindowAsync(
                notification.UserId,
                notification.Type,
                notification.ReferenceType,
                notification.ReferenceId,
                DateTime.UtcNow.AddMinutes(-5));

            if (alreadyExists)
            {
                _logger.LogInformation(
                    "Notification duplicate ignored for user {UserId}, type {Type}, reference {ReferenceType}/{ReferenceId}.",
                    notification.UserId,
                    notification.Type,
                    notification.ReferenceType,
                    notification.ReferenceId);
                return;
            }

            notification.Id = await _notificationRepository.CreateAsync(notification);
            if (notification.Id <= 0)
            {
                _logger.LogDebug(
                    "Notification duplicate skipped after insert race for user {UserId}, type {Type}, reference {ReferenceType}/{ReferenceId}.",
                    notification.UserId,
                    notification.Type,
                    notification.ReferenceType,
                    notification.ReferenceId);
                return;
            }

            var unreadCount = await _notificationRepository.GetUnreadCountAsync(notification.UserId, notification.OrganizationId);
            await _signalRNotificationService.SendToUserAsync(notification, unreadCount);

            var pushDispatch = await _pushNotificationService.SendAsync(notification, cancellationToken);
            if (pushDispatch.IsSent)
            {
                notification.IsPushSent = true;
                notification.Channel = pushDispatch.Channel;
                notification.ExternalProviderId = pushDispatch.ExternalProviderId;
                await _notificationRepository.MarkPushSentAsync(
                    notification.Id,
                    pushDispatch.ExternalProviderId,
                    pushDispatch.Channel);
            }

            _logger.LogInformation(
                "Notification stored (pushSent={PushSent}) for user {UserId} with type {Type}.",
                pushDispatch.IsSent,
                notification.UserId,
                notification.Type);
        }

        private static void ValidateMessage(NotificationEventMessage message)
        {
            if (message == null)
            {
                throw new ServiceException("Message de notification invalide.");
            }

            if (message.UserId <= 0)
            {
                throw new ServiceException("UserId invalide pour la notification.");
            }

            if (string.IsNullOrWhiteSpace(message.Type))
            {
                throw new ServiceException("Le type de notification est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(message.Category))
            {
                throw new ServiceException("La categorie de notification est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(message.Title))
            {
                throw new ServiceException("Le titre de notification est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(message.Message))
            {
                throw new ServiceException("Le message de notification est obligatoire.");
            }
        }

        private static int ResolveOrganizationId(NotificationEventMessage message, User user)
        {
            if (message.OrganizationId.HasValue)
            {
                return message.OrganizationId.Value;
            }

            if (user.OrganizationId.HasValue)
            {
                return user.OrganizationId.Value;
            }

            throw new ServiceException("Impossible de determiner l'organisation de la notification.");
        }

        private static string NormalizeAndValidate(
            string value,
            ISet<string> allowedValues,
            string fieldName,
            string? fallback = null)
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ServiceException($"{fieldName} de notification obligatoire.");
            }

            if (!allowedValues.Contains(normalized))
            {
                if (!string.IsNullOrWhiteSpace(fallback) && allowedValues.Contains(fallback))
                {
                    return fallback;
                }

                throw new ServiceException($"{fieldName} de notification invalide: {normalized}.");
            }

            return normalized;
        }
    }
}
