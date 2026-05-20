using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DocApi.Common;
using DocApi.Infrastructure;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocApi.Services
{
    public sealed class OpenRouterService : IOpenRouterService
    {
        private readonly HttpClient _httpClient;
        private readonly OpenRouterSettings _settings;
        private readonly ILogger<OpenRouterService> _logger;

        public OpenRouterService(
            HttpClient httpClient,
            IOptions<OpenRouterSettings> settings,
            ILogger<OpenRouterService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<string> GenerateAnswerAsync(
            string systemPrompt,
            string question,
            string context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                throw new ServiceException("La cle API Groq n'est pas configuree.");
            }

            var payload = new
            {
                model = string.IsNullOrWhiteSpace(_settings.Model) ? "llama-3.3-70b-versatile" : _settings.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = BuildUserPayload(question, context) }
                },
                temperature = _settings.Temperature,
                max_tokens = _settings.MaxOutputTokens
            };

            var endpoint = $"{_settings.ApiBaseUrl.TrimEnd('/')}/chat/completions";
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey.Trim());

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Groq API error {StatusCode} | URL: {Url} | Model: {Model} | Response: {Body}",
                    (int)response.StatusCode,
                    endpoint,
                    _settings.Model,
                    body);
                throw new ServiceException($"Echec de communication avec Groq (HTTP {(int)response.StatusCode}).");
            }

            var answer = ExtractAnswer(body);
            if (string.IsNullOrWhiteSpace(answer))
            {
                throw new ServiceException("La reponse Groq est vide.");
            }

            return answer.Trim();
        }

        private static string BuildUserPayload(string question, string context)
        {
            var builder = new StringBuilder();
            builder.AppendLine("QUESTION UTILISATEUR:");
            builder.AppendLine(question);
            builder.AppendLine();
            builder.AppendLine("CONTEXTE QUALIFLOW / ISO:");
            builder.AppendLine(string.IsNullOrWhiteSpace(context) ? "Aucun contexte fourni." : context);
            return builder.ToString();
        }

        private static string ExtractAnswer(string responseBody)
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content))
            {
                return string.Empty;
            }

            return content.GetString() ?? string.Empty;
        }
    }
}
