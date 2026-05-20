using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DocApi.Services
{
    public sealed class NotificationDispatcherService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ConnectionFactory _connectionFactory;
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<NotificationDispatcherService> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public NotificationDispatcherService(
            IServiceScopeFactory scopeFactory,
            ConnectionFactory connectionFactory,
            IOptions<RabbitMqSettings> settings,
            ILogger<NotificationDispatcherService> logger)
        {
            _scopeFactory = scopeFactory;
            _connectionFactory = connectionFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var connection = _connectionFactory.CreateConnection();
                    using var channel = connection.CreateModel();

                    channel.QueueDeclare(
                        queue: _settings.QueueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    channel.BasicQos(0, 1, false);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.Received += async (_, eventArgs) =>
                    {
                        try
                        {
                            var message = DeserializeMessage(eventArgs.Body.ToArray());
                            using var scope = _scopeFactory.CreateScope();
                            var notificationConsumer = scope.ServiceProvider.GetRequiredService<INotificationConsumerService>();

                            await notificationConsumer.HandleAsync(message, stoppingToken);
                            channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
                        }
                        catch (OperationCanceledException)
                        {
                            channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erreur lors du traitement d'une notification RabbitMQ.");
                            channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: true);
                        }
                    };

                    channel.BasicConsume(
                        queue: _settings.QueueName,
                        autoAck: false,
                        consumer: consumer);

                    _logger.LogInformation("Notification dispatcher listening on queue {QueueName}.", _settings.QueueName);

                    await WaitForCancellationAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RabbitMQ indisponible pour le dispatcher notifications. Nouvelle tentative dans 5 secondes.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        private static NotificationEventMessage DeserializeMessage(byte[] body)
        {
            var json = Encoding.UTF8.GetString(body);
            var message = JsonSerializer.Deserialize<NotificationEventMessage>(json, JsonOptions);
            if (message == null)
            {
                throw new ServiceException("Message RabbitMQ invalide.");
            }

            return message;
        }

        private static Task WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
