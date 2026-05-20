using System.Threading;
using System.Threading.Tasks;
using DocApi.Domain.Entities;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocApi.Services
{
    public sealed class InlineNotificationPublisher : INotificationPublisher
    {
        private readonly INotificationConsumerService _notificationConsumerService;
        private readonly ILogger<InlineNotificationPublisher> _logger;

        public InlineNotificationPublisher(
            INotificationConsumerService notificationConsumerService,
            ILogger<InlineNotificationPublisher> logger)
        {
            _notificationConsumerService = notificationConsumerService;
            _logger = logger;
        }

        public async Task PublishAsync(NotificationEventMessage message, CancellationToken cancellationToken = default)
        {
            await _notificationConsumerService.HandleAsync(message, cancellationToken);

            _logger.LogDebug(
                "Notification traitée en mode inline pour user {UserId} ({Type}).",
                message.UserId,
                message.Type);
        }
    }
}
