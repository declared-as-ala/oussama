using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DocApi.Infrastructure;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocApi.Services
{
    public class ILovePdfService : IConversionService
    {
        private readonly ILovePdfSettings _settings;
        private readonly ILogger<ILovePdfService> _logger;
        private readonly HttpClient _httpClient;

        public ILovePdfService(
            IOptions<ILovePdfSettings> settings,
            ILogger<ILovePdfService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(3);
        }

        public async Task<(Stream Stream, string ContentType, string FileName)> ConvertAsync(
            Stream sourceStream,
            string sourceContentType,
            string sourceFileName,
            string targetFormat)
        {
            targetFormat = targetFormat.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(_settings.PublicKey))
            {
                _logger.LogWarning("iLovePDF Public Key is not configured. Returning original file.");
                throw new InvalidOperationException("La conversion de document n'est pas configurée (clé API iLovePDF manquante).");
            }

            var extension = Path.GetExtension(sourceFileName).ToLowerInvariant();
            var isSourcePdf = extension == ".pdf" || sourceContentType == "application/pdf";
            var isSourceWord = extension == ".docx" || extension == ".doc" || sourceContentType.Contains("word") || sourceContentType.Contains("officedocument.wordprocessingml");
            var isSourceExcel = extension == ".xlsx" || extension == ".xls" || sourceContentType.Contains("excel") || sourceContentType.Contains("officedocument.spreadsheetml");
            var isSourceImage = extension == ".jpg" || extension == ".jpeg" || extension == ".png" || sourceContentType.StartsWith("image/");

            // Standardize target format
            string targetExt = targetFormat switch
            {
                "pdf" => ".pdf",
                "word" or "docx" => ".docx",
                "excel" or "xlsx" => ".xlsx",
                "jpg" or "jpeg" => ".jpg",
                "png" => ".png",
                _ => throw new ArgumentException($"Format cible non pris en charge: {targetFormat}")
            };

            // If target is same as source, return original
            if ((targetExt == ".pdf" && isSourcePdf) ||
                (targetExt == ".docx" && isSourceWord) ||
                (targetExt == ".xlsx" && isSourceExcel) ||
                ((targetExt == ".jpg" || targetExt == ".png") && isSourceImage))
            {
                return (sourceStream, sourceContentType, sourceFileName);
            }

            _logger.LogInformation("Converting {FileName} ({ContentType}) to {TargetFormat} via iLovePDF", sourceFileName, sourceContentType, targetFormat);

            // Determine conversion tool chain
            var tools = new List<string>();
            if (isSourceWord)
            {
                tools.Add("officepdf"); // Word to PDF first
                if (targetExt == ".xlsx") tools.Add("pdfexcel");
                else if (targetExt == ".jpg" || targetExt == ".png") tools.Add("pdfjpg");
            }
            else if (isSourceExcel)
            {
                tools.Add("officepdf"); // Excel to PDF first
                if (targetExt == ".docx") tools.Add("pdfword");
                else if (targetExt == ".jpg" || targetExt == ".png") tools.Add("pdfjpg");
            }
            else if (isSourceImage)
            {
                tools.Add("imagepdf"); // Image to PDF first
                if (targetExt == ".docx") tools.Add("pdfword");
                else if (targetExt == ".xlsx") tools.Add("pdfexcel");
            }
            else if (isSourcePdf)
            {
                if (targetExt == ".docx") tools.Add("pdfword");
                else if (targetExt == ".xlsx") tools.Add("pdfexcel");
                else if (targetExt == ".jpg" || targetExt == ".png") tools.Add("pdfjpg");
            }
            else
            {
                // Fallback attempt: try treating as PDF or throw
                tools.Add("pdfword");
            }

            // Authenticate and get JWT Token
            var token = await AuthenticateAsync();

            Stream currentStream = sourceStream;
            string currentFileName = sourceFileName;
            string currentContentType = sourceContentType;

            for (int i = 0; i < tools.Count; i++)
            {
                var tool = tools[i];
                var isLastTool = i == tools.Count - 1;
                var currentTargetFormat = isLastTool ? targetFormat : "pdf";

                var stepResult = await ExecuteConversionStepAsync(currentStream, currentFileName, tool, currentTargetFormat, token);
                currentStream = stepResult.Stream;
                currentContentType = stepResult.ContentType;
                
                // Update file properties for next step
                var stepExt = currentContentType.Contains("zip") ? ".zip" : (currentTargetFormat == "pdf" ? ".pdf" : targetExt);
                currentFileName = Path.GetFileNameWithoutExtension(currentFileName) + stepExt;
            }

            var finalFileName = Path.ChangeExtension(sourceFileName, Path.GetExtension(currentFileName));
            return (currentStream, currentContentType, finalFileName);
        }

        private async Task<string> AuthenticateAsync()
        {
            var response = await _httpClient.PostAsJsonAsync("https://api.ilovepdf.com/v1/auth", new
            {
                public_key = _settings.PublicKey
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("iLovePDF Authentication failed: {Error}", errorContent);
                throw new InvalidOperationException("Échec de l'authentification avec l'API iLovePDF. Vérifiez votre clé API.");
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (string.IsNullOrEmpty(result?.Token))
            {
                throw new InvalidOperationException("Le jeton d'authentification iLovePDF retourné est vide.");
            }

            return result.Token;
        }

        private async Task<(Stream Stream, string ContentType)> ExecuteConversionStepAsync(
            Stream sourceStream,
            string fileName,
            string tool,
            string targetFormat,
            string token)
        {
            // 1. Start Task
            using var startRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.ilovepdf.com/v1/start/{tool}");
            startRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var startResponse = await _httpClient.SendAsync(startRequest);
            if (!startResponse.IsSuccessStatusCode)
            {
                var err = await startResponse.Content.ReadAsStringAsync();
                _logger.LogError("iLovePDF start task {Tool} failed: {Error}", tool, err);
                throw new InvalidOperationException($"Impossible d'initialiser la tâche de conversion iLovePDF ({tool}).");
            }

            var taskData = await startResponse.Content.ReadFromJsonAsync<StartResponse>();
            if (taskData == null || string.IsNullOrEmpty(taskData.Server) || string.IsNullOrEmpty(taskData.TaskId))
            {
                throw new InvalidOperationException("Données de tâche iLovePDF invalides reçues.");
            }

            var serverUrl = $"https://{taskData.Server}";

            // 2. Upload file
            byte[] fileBytes;
            if (sourceStream.CanSeek)
            {
                sourceStream.Position = 0;
            }
            using (var ms = new MemoryStream())
            {
                await sourceStream.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            using var uploadContent = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            uploadContent.Add(fileContent, "file", fileName);
            uploadContent.Add(new StringContent(taskData.TaskId), "task");

            using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"{serverUrl}/v1/upload");
            uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            uploadRequest.Content = uploadContent;

            var uploadResponse = await _httpClient.SendAsync(uploadRequest);
            if (!uploadResponse.IsSuccessStatusCode)
            {
                var err = await uploadResponse.Content.ReadAsStringAsync();
                _logger.LogError("iLovePDF upload file failed: {Error}", err);
                throw new InvalidOperationException("Échec du téléversement du fichier vers iLovePDF.");
            }

            var uploadData = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
            if (uploadData == null || string.IsNullOrEmpty(uploadData.ServerFilename))
            {
                throw new InvalidOperationException("Nom de fichier serveur iLovePDF manquant.");
            }

            // 3. Process task
            var processBody = new Dictionary<string, object>
            {
                { "task", taskData.TaskId },
                { "tool", tool },
                { "files", new[]
                    {
                        new { server_filename = uploadData.ServerFilename, filename = fileName }
                    }
                }
            };

            // If we are using pdfjpg and want png format
            if (tool == "pdfjpg" && targetFormat == "png")
            {
                processBody["pdfjpg_format"] = "png";
                processBody["output_format"] = "png";
            }

            using var processRequest = new HttpRequestMessage(HttpMethod.Post, $"{serverUrl}/v1/process");
            processRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            processRequest.Content = JsonContent.Create(processBody);

            var processResponse = await _httpClient.SendAsync(processRequest);
            if (!processResponse.IsSuccessStatusCode)
            {
                var err = await processResponse.Content.ReadAsStringAsync();
                _logger.LogError("iLovePDF process failed: {Error}", err);
                throw new InvalidOperationException("Échec du traitement du fichier par iLovePDF.");
            }

            // 4. Download file
            using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, $"{serverUrl}/v1/download/{taskData.TaskId}");
            downloadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var downloadResponse = await _httpClient.SendAsync(downloadRequest);
            if (!downloadResponse.IsSuccessStatusCode)
            {
                var err = await downloadResponse.Content.ReadAsStringAsync();
                _logger.LogError("iLovePDF download failed: {Error}", err);
                throw new InvalidOperationException("Échec du téléchargement du fichier converti depuis iLovePDF.");
            }

            var contentType = downloadResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var resultMs = new MemoryStream();
            await downloadResponse.Content.CopyToAsync(resultMs);
            resultMs.Position = 0;
            return (resultMs, contentType);
        }

        private class AuthResponse
        {
            [JsonPropertyName("token")]
            public string Token { get; set; } = string.Empty;
        }

        private class StartResponse
        {
            [JsonPropertyName("server")]
            public string Server { get; set; } = string.Empty;

            [JsonPropertyName("task")]
            public string TaskId { get; set; } = string.Empty;
        }

        private class UploadResponse
        {
            [JsonPropertyName("server_filename")]
            public string ServerFilename { get; set; } = string.Empty;
        }
    }
}
