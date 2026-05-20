using DocApi.Domain.Entities;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocApi.Services
{
    public sealed class NoopNotificationPublisher : INotificationPublisher
    {
        private readonly ILogger<NoopNotificationPublisher> _logger;

        public NoopNotificationPublisher(ILogger<NoopNotificationPublisher> logger)
        {
            _logger = logger;
        }

        public Task PublishAsync(NotificationEventMessage message, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug(
                "RabbitMQ disabled. Notification event skipped for user {UserId} ({Type}).",
                message.UserId,
                message.Type);

            return Task.CompletedTask;
        }
    }
}
