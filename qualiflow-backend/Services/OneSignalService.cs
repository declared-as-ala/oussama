using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Infrastructure;
using DocApi.Services.Interfaces;
using DocApi.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocApi.Services
{
    public sealed class OneSignalService : IOneSignalService
    {
        private readonly HttpClient _httpClient;
        private readonly OneSignalSettings _settings;
        private readonly ILogger<OneSignalService> _logger;

        public OneSignalService(
            HttpClient httpClient,
            IOptions<OneSignalSettings> options,
            ILogger<OneSignalService> logger)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _logger = logger;
        }

        public Task<OneSignalSendResult> SendToExternalIdsAsync(
            IReadOnlyCollection<string> externalIds,
            string title,
            string message,
            IDictionary<string, string>? data = null,
            CancellationToken cancellationToken = default)
        {
            var cleanedExternalIds = externalIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (cleanedExternalIds.Length == 0)
            {
                return Task.FromResult(new OneSignalSendResult
                {
                    IsSuccess = false,
                    Error = "Aucun external_id valide fourni."
                });
            }

            var payload = new Dictionary<string, object?>
            {
                ["app_id"] = _settings.AppId,
                ["target_channel"] = "push",
                ["include_aliases"] = new Dictionary<string, object?>
                {
                    ["external_id"] = cleanedExternalIds
                },
                ["headings"] = CreateLocalizedText(title),
                ["contents"] = CreateLocalizedText(message),
                ["data"] = data ?? new Dictionary<string, string>()
            };

            return SendAsync(payload, cancellationToken);
        }

        public Task<OneSignalSendResult> SendByTagsAsync(
            IReadOnlyDictionary<string, string> tags,
            string title,
            string message,
            IDictionary<string, string>? data = null,
            CancellationToken cancellationToken = default)
        {
            var normalizedTags = tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag.Key) && !string.IsNullOrWhiteSpace(tag.Value))
                .Select(tag => new KeyValuePair<string, string>(tag.Key.Trim(), tag.Value.Trim()))
                .ToArray();

            if (normalizedTags.Length == 0)
            {
                return Task.FromResult(new OneSignalSendResult
                {
                    IsSuccess = false,
                    Error = "Aucun tag valide fourni."
                });
            }

            var filters = new List<Dictionary<string, string>>();
            for (var index = 0; index < normalizedTags.Length; index++)
            {
                var pair = normalizedTags[index];
                filters.Add(new Dictionary<string, string>
                {
                    ["field"] = "tag",
                    ["key"] = pair.Key,
                    ["relation"] = "=",
                    ["value"] = pair.Value
                });

                if (index < normalizedTags.Length - 1)
                {
                    filters.Add(new Dictionary<string, string>
                    {
                        ["operator"] = "AND"
                    });
                }
            }

            var payload = new Dictionary<string, object?>
            {
                ["app_id"] = _settings.AppId,
                ["target_channel"] = "push",
                ["filters"] = filters,
                ["headings"] = CreateLocalizedText(title),
                ["contents"] = CreateLocalizedText(message),
                ["data"] = data ?? new Dictionary<string, string>()
            };

            return SendAsync(payload, cancellationToken);
        }

        private async Task<OneSignalSendResult> SendAsync(
            IDictionary<string, object?> payload,
            CancellationToken cancellationToken)
        {
            if (!_settings.Enabled)
            {
                return new OneSignalSendResult { IsSuccess = false, Error = "OneSignal desactive." };
            }

            if (string.IsNullOrWhiteSpace(_settings.AppId) || string.IsNullOrWhiteSpace(_settings.RestApiKey))
            {
                return new OneSignalSendResult { IsSuccess = false, Error = "Configuration OneSignal incomplete." };
            }

            var endpointBase = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl)
                ? "https://api.onesignal.com"
                : _settings.ApiBaseUrl.Trim().TrimEnd('/');

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpointBase}/notifications?c=push")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Key", _settings.RestApiKey.Trim());

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "OneSignal returned {StatusCode}. Body={Body}",
                        (int)response.StatusCode,
                        body);
                    return new OneSignalSendResult
                    {
                        IsSuccess = false,
                        Error = $"OneSignal error {(int)response.StatusCode}."
                    };
                }

                var parsed = JsonSerializer.Deserialize<OneSignalCreateNotificationResponse>(body);
                return new OneSignalSendResult
                {
                    IsSuccess = !string.IsNullOrWhiteSpace(parsed?.Id),
                    NotificationId = parsed?.Id,
                    Error = string.IsNullOrWhiteSpace(parsed?.Id) ? "OneSignal id manquant." : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OneSignal push send failed.");
                return new OneSignalSendResult
                {
                    IsSuccess = false,
                    Error = "Erreur interne lors de l'envoi OneSignal."
                };
            }
        }

        private Dictionary<string, string> CreateLocalizedText(string text)
        {
            var language = string.IsNullOrWhiteSpace(_settings.DefaultLanguage)
                ? "en"
                : _settings.DefaultLanguage.Trim().ToLowerInvariant();

            return new Dictionary<string, string>
            {
                [language] = string.IsNullOrWhiteSpace(text) ? "-" : text.Trim()
            };
        }

        private sealed class OneSignalCreateNotificationResponse
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }
        }
    }
}
