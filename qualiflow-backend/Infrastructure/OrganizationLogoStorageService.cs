using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace DocApi.Infrastructure
{
    public class OrganizationLogoStorageService : IOrganizationLogoStorageService
    {
        private readonly string _rootPath;
        private readonly long _maxFileSizeBytes;
        private readonly HashSet<string> _allowedExtensions;

        public OrganizationLogoStorageService(IConfiguration configuration)
        {
            var configuredPath = configuration["Storage:OrganizationLogosPath"];
            _rootPath = ResolveRootPath(configuredPath);
            var maxMb = configuration.GetValue("Storage:OrganizationLogoMaxFileSizeMb", 5);
            _maxFileSizeBytes = Math.Max(maxMb, 1) * 1024L * 1024L;

            _allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg"
            };

            Directory.CreateDirectory(_rootPath);
        }

        public async Task<string> SaveAsync(IFormFile file, string organizationCode, CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length <= 0)
            {
                throw new ServiceException("Le logo est obligatoire.");
            }

            if (file.Length > _maxFileSizeBytes)
            {
                throw new ServiceException($"Le logo depasse la taille maximale ({_maxFileSizeBytes / 1024 / 1024} MB).");
            }

            var extension = Path.GetExtension(file.FileName)?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || !_allowedExtensions.Contains(extension))
            {
                throw new ServiceException("Format logo non autorise. Utilise .png, .jpg ou .jpeg.");
            }

            var sanitizedCode = SanitizeFileName(organizationCode).ToUpperInvariant();
            var fileName = $"{sanitizedCode}{extension}";
            var absolutePath = Path.Combine(_rootPath, fileName);

            foreach (var existingPath in Directory.GetFiles(_rootPath, $"{sanitizedCode}.*"))
            {
                if (!existingPath.Equals(absolutePath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(existingPath);
                }
            }

            await using (var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            return Path.Combine("organization-logos", fileName).Replace('\\', '/');
        }

        public Task<(Stream Stream, string ContentType, string FileName)> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new NotFoundException("Logo introuvable.");
            }

            var fileName = Path.GetFileName(relativePath);
            var absolutePath = Path.Combine(_rootPath, fileName);
            if (!File.Exists(absolutePath))
            {
                throw new NotFoundException("Logo introuvable.");
            }

            var extension = Path.GetExtension(absolutePath).ToLowerInvariant();
            var contentType = extension switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };

            Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult((stream, contentType, fileName));
        }

        private static string ResolveRootPath(string? configuredPath)
        {
            var path = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine("StorageFiles", "organization-logos")
                : configuredPath.Trim();

            return Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "ORG";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = value.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (invalidChars.Contains(chars[i]) || char.IsWhiteSpace(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }
    }
}
