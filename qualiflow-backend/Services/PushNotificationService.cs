using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Services.Interfaces;
using DocApi.Services.Models;
using Microsoft.Extensions.Logging;
using DomainNotification = DocApi.Domain.Entities.Notification;

namespace DocApi.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly IOneSignalService _oneSignalService;
        private readonly ILogger<PushNotificationService> _logger;

        public PushNotificationService(
            IOneSignalService oneSignalService,
            ILogger<PushNotificationService> logger)
        {
            _oneSignalService = oneSignalService;
            _logger = logger;
        }

        public async Task<PushDispatchResult> SendAsync(DomainNotification notification, CancellationToken cancellationToken = default)
        {
            if (notification == null)
            {
                return new PushDispatchResult { IsSent = false };
            }

            if (notification.UserId <= 0)
            {
                return new PushDispatchResult { IsSent = false };
            }

            var payload = new Dictionary<string, string>
            {
                ["redirectUrl"] = notification.RedirectUrl ?? notification.ActionUrl ?? string.Empty,
                ["entityId"] = notification.EntityId?.ToString() ?? notification.ReferenceId ?? string.Empty,
                ["entityType"] = notification.EntityType ?? notification.ReferenceType ?? string.Empty,
                ["notificationId"] = notification.Id.ToString()
            };

            var oneSignalResult = await _oneSignalService.SendToExternalIdsAsync(
                new[] { notification.UserId.ToString() },
                notification.Title,
                notification.Message,
                payload,
                cancellationToken);

            if (!oneSignalResult.IsSuccess)
            {
                var error = oneSignalResult.Error ?? "unknown";

                if (error.Contains("desactive", StringComparison.OrdinalIgnoreCase)
                    || error.Contains("incomplete", StringComparison.OrdinalIgnoreCase)
                    || error.Contains("incompl", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug(
                        "OneSignal disabled or not configured. Push skipped for UserId={UserId}. Error={Error}",
                        notification.UserId,
                        error);
                }
                else
                {
                    _logger.LogWarning(
                        "OneSignal push send failed for UserId={UserId}. Error={Error}",
                        notification.UserId,
                        error);
                }
            }

            return new PushDispatchResult
            {
                IsSent = oneSignalResult.IsSuccess,
                Channel = "PUSH",
                ExternalProviderId = oneSignalResult.NotificationId
            };
        }
    }
}
