using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;

namespace DocApi.Services
{
    public sealed class NotificationEventPublisher : INotificationEventPublisher
    {
        private readonly INotificationPublisher _notificationPublisher;
        private readonly IUserRepository _userRepository;

        public NotificationEventPublisher(
            INotificationPublisher notificationPublisher,
            IUserRepository userRepository)
        {
            _notificationPublisher = notificationPublisher;
            _userRepository = userRepository;
        }

        public Task PublishToUserAsync(
            int organizationId,
            int userId,
            string type,
            string category,
            string title,
            string message,
            string priority,
            string? referenceType,
            string? referenceId,
            string? actionUrl,
            int? triggeredByUserId = null,
            CancellationToken cancellationToken = default)
        {
            return PublishToUsersAsync(
                organizationId,
                new[] { userId },
                type,
                category,
                title,
                message,
                priority,
                referenceType,
                referenceId,
                actionUrl,
                triggeredByUserId,
                cancellationToken);
        }

        public async Task PublishToUsersAsync(
            int organizationId,
            IEnumerable<int> userIds,
            string type,
            string category,
            string title,
            string message,
            string priority,
            string? referenceType,
            string? referenceId,
            string? actionUrl,
            int? triggeredByUserId = null,
            CancellationToken cancellationToken = default)
        {
            if (organizationId <= 0)
            {
                return;
            }

            var targetUserIds = userIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (targetUserIds.Count == 0)
            {
                return;
            }

            var users = (await _userRepository.GetByIdsAsync(organizationId, targetUserIds))
                .Where(user => user.IsActive && user.OrganizationId == organizationId)
                .ToList();

            if (users.Count == 0)
            {
                return;
            }

            var normalizedType = NormalizeAndValidate(type, NotificationConstants.AllowedTypes, NotificationConstants.TypeSystemAlert);
            var normalizedCategory = NormalizeAndValidate(category, NotificationConstants.AllowedCategories, NotificationConstants.CategoryInfo);
            var normalizedPriority = NormalizeAndValidate(priority, NotificationConstants.AllowedPriorities, NotificationConstants.PriorityMedium);
            var normalizedTitle = NormalizeRequiredText(title, 255);
            var normalizedMessage = NormalizeRequiredText(message, 2000);
            var normalizedReferenceType = NormalizeOptionalText(referenceType, 80);
            var normalizedReferenceId = NormalizeOptionalText(referenceId, 80);
            var normalizedActionUrl = NormalizeOptionalText(actionUrl, 500);

            foreach (var user in users)
            {
                var payload = new NotificationEventMessage
                {
                    OrganizationId = organizationId,
                    UserId = user.Id,
                    SenderId = triggeredByUserId,
                    Type = normalizedType,
                    Category = normalizedCategory,
                    Title = normalizedTitle,
                    Message = normalizedMessage,
                    Priority = normalizedPriority,
                    EntityType = normalizedReferenceType,
                    EntityId = int.TryParse(normalizedReferenceId, out var entityId) ? entityId : null,
                    RedirectUrl = normalizedActionUrl,
                    ReferenceType = normalizedReferenceType,
                    ReferenceId = normalizedReferenceId,
                    ActionUrl = normalizedActionUrl,
                    TriggeredByUserId = triggeredByUserId,
                    TriggeredAt = DateTime.UtcNow
                };

                await _notificationPublisher.PublishAsync(payload, cancellationToken);
            }
        }

        public async Task PublishToRolesAsync(
            int organizationId,
            IEnumerable<string> roles,
            string type,
            string category,
            string title,
            string message,
            string priority,
            string? referenceType,
            string? referenceId,
            string? actionUrl,
            int? triggeredByUserId = null,
            CancellationToken cancellationToken = default)
        {
            if (organizationId <= 0)
            {
                return;
            }

            var normalizedRoles = roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            if (normalizedRoles.Count == 0)
            {
                return;
            }

            var users = await _userRepository.GetActiveByOrganizationAndRolesAsync(organizationId, normalizedRoles);
            var targetIds = users.Select(user => user.Id).Distinct().ToList();

            if (targetIds.Count == 0)
            {
                return;
            }

            await PublishToUsersAsync(
                organizationId,
                targetIds,
                type,
                category,
                title,
                message,
                priority,
                referenceType,
                referenceId,
                actionUrl,
                triggeredByUserId,
                cancellationToken);
        }

        private static string NormalizeAndValidate(string value, ISet<string> allowedValues, string fallback)
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim().ToUpperInvariant();

            if (!allowedValues.Contains(normalized))
            {
                return fallback;
            }

            return normalized;
        }

        private static string NormalizeRequiredText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ServiceException("Les informations de notification sont invalides.");
            }

            var normalized = value.Trim();
            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized.Substring(0, maxLength);
        }

        private static string? NormalizeOptionalText(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim();
            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized.Substring(0, maxLength);
        }
    }
}
