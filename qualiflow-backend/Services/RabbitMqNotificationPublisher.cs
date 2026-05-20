using System.Text;
using System.Text.Json;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DocApi.Services
{
    public sealed class RabbitMqNotificationPublisher : INotificationPublisher
    {
        private readonly ConnectionFactory _connectionFactory;
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<RabbitMqNotificationPublisher> _logger;

        public RabbitMqNotificationPublisher(
            ConnectionFactory connectionFactory,
            IOptions<RabbitMqSettings> settings,
            ILogger<RabbitMqNotificationPublisher> logger)
        {
            _connectionFactory = connectionFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        public Task PublishAsync(NotificationEventMessage message, CancellationToken cancellationToken = default)
        {
            using var connection = _connectionFactory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(
                queue: _settings.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";

            channel.BasicPublish(
                exchange: string.Empty,
                routingKey: _settings.QueueName,
                basicProperties: properties,
                body: body);

            _logger.LogInformation(
                "Notification event published to RabbitMQ queue {QueueName} for user {UserId} ({Type}).",
                _settings.QueueName,
                message.UserId,
                message.Type);

            return Task.CompletedTask;
        }
    }
}
