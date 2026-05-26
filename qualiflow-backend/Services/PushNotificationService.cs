using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;
using DocApi.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DomainNotification = DocApi.Domain.Entities.Notification;

namespace DocApi.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        private static readonly object FirebaseAppLock = new();
        private readonly IUserDeviceRepository _userDeviceRepository;
        private readonly FirebaseSettings _settings;
        private readonly ILogger<PushNotificationService> _logger;

        public PushNotificationService(
            IUserDeviceRepository userDeviceRepository,
            IOptions<FirebaseSettings> options,
            ILogger<PushNotificationService> logger)
        {
            _userDeviceRepository = userDeviceRepository;
            _settings = options.Value;
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

            if (!_settings.Enabled)
            {
                _logger.LogDebug("FCM disabled. Push skipped for UserId={UserId}.", notification.UserId);
                return new PushDispatchResult { IsSent = false, Channel = "FCM" };
            }

            if (!TryEnsureFirebaseApp())
            {
                return new PushDispatchResult { IsSent = false, Channel = "FCM" };
            }

            var devices = (await _userDeviceRepository.GetActiveByUserIdAsync(notification.UserId)).ToArray();
            var tokens = devices
                .Select(device => device.DeviceToken)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (tokens.Length == 0)
            {
                _logger.LogDebug("No active FCM device tokens for UserId={UserId}.", notification.UserId);
                return new PushDispatchResult { IsSent = false, Channel = "FCM" };
            }

            var data = new Dictionary<string, string>
            {
                ["redirectUrl"] = notification.RedirectUrl ?? notification.ActionUrl ?? string.Empty,
                ["entityId"] = notification.EntityId?.ToString() ?? notification.ReferenceId ?? string.Empty,
                ["entityType"] = notification.EntityType ?? notification.ReferenceType ?? string.Empty,
                ["notificationId"] = notification.Id.ToString(),
                ["type"] = notification.Type ?? string.Empty
            };

            var multicast = new MulticastMessage
            {
                Tokens = tokens,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = string.IsNullOrWhiteSpace(notification.Title) ? "QualiFlow" : notification.Title,
                    Body = string.IsNullOrWhiteSpace(notification.Message) ? "Nouvelle notification." : notification.Message
                },
                Data = data,
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = "qualiflow_alerts",
                        Sound = "default"
                    }
                }
            };

            try
            {
                var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(multicast, cancellationToken);
                _logger.LogInformation(
                    "FCM push sent for UserId={UserId}. Success={SuccessCount}, Failure={FailureCount}",
                    notification.UserId,
                    response.SuccessCount,
                    response.FailureCount);

                return new PushDispatchResult
                {
                    IsSent = response.SuccessCount > 0,
                    Channel = "FCM",
                    ExternalProviderId = response.SuccessCount > 0 ? $"{response.SuccessCount}/{tokens.Length}" : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FCM push send failed for UserId={UserId}.", notification.UserId);
                return new PushDispatchResult { IsSent = false, Channel = "FCM" };
            }
        }

        private bool TryEnsureFirebaseApp()
        {
            if (FirebaseApp.DefaultInstance != null)
            {
                return true;
            }

            lock (FirebaseAppLock)
            {
                if (FirebaseApp.DefaultInstance != null)
                {
                    return true;
                }

                try
                {
                    var credential = CreateCredential();
                    if (credential == null)
                    {
                        _logger.LogWarning("Firebase configuration incomplete. Push skipped.");
                        return false;
                    }

                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = credential,
                        ProjectId = string.IsNullOrWhiteSpace(_settings.ProjectId) ? null : _settings.ProjectId.Trim()
                    });

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Firebase initialization failed.");
                    return false;
                }
            }
        }

        private GoogleCredential? CreateCredential()
        {
            if (!string.IsNullOrWhiteSpace(_settings.ServiceAccountJson))
            {
                return CredentialFactory.FromJson(
                    _settings.ServiceAccountJson,
                    JsonCredentialParameters.ServiceAccountCredentialType);
            }

            var path = ResolveServiceAccountPath(_settings.ServiceAccountPath);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            return CredentialFactory.FromFile(path, JsonCredentialParameters.ServiceAccountCredentialType);
        }

        private static string? ResolveServiceAccountPath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return null;
            }

            var trimmedPath = configuredPath.Trim();
            if (Path.IsPathRooted(trimmedPath))
            {
                return trimmedPath;
            }

            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), trimmedPath),
                Path.Combine(AppContext.BaseDirectory, trimmedPath),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", trimmedPath)
            };

            return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
        }
    }
}
