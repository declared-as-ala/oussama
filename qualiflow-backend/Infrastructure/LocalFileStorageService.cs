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
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly string _rootPath;
        private readonly HashSet<string> _allowedExtensions;
        private readonly long _maxFileSizeBytes;

        public LocalFileStorageService(IConfiguration configuration)
        {
            var configuredRoot = configuration["Storage:RootPath"];
            _rootPath = ResolveRootPath(configuredRoot);

            var extensions = configuration["Storage:AllowedExtensions"];
            _allowedExtensions = ParseAllowedExtensions(extensions);

            var maxMb = configuration.GetValue("Storage:MaxFileSizeMb", 20);
            _maxFileSizeBytes = Math.Max(maxMb, 1) * 1024L * 1024L;

            Directory.CreateDirectory(_rootPath);
        }

        public async Task<StoredFileInfo> SaveAsync(
            IFormFile file,
            string organizationSegment,
            string documentCode,
            string versionNumber,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length <= 0)
            {
                throw new ServiceException("Le fichier a televerser est obligatoire.");
            }

            if (file.Length > _maxFileSizeBytes)
            {
                throw new ServiceException($"La taille du fichier depasse la limite autorisee ({_maxFileSizeBytes / 1024 / 1024} MB).");
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                throw new ServiceException("Le fichier doit avoir une extension valide.");
            }

            var normalizedExtension = extension.Trim().ToLowerInvariant();
            if (_allowedExtensions.Count > 0 && !_allowedExtensions.Contains(normalizedExtension))
            {
                throw new ServiceException("Le type de fichier n'est pas autorise.");
            }

            var safeOrgSegment = SanitizeSegment(organizationSegment);
            var safeCode = SanitizeSegment(documentCode);
            var safeVersion = SanitizeSegment(versionNumber);

            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{normalizedExtension}";
            var relativePath = Path.Combine("documents", safeOrgSegment, safeCode, safeVersion, fileName);
            var absolutePath = Path.Combine(_rootPath, relativePath);
            var targetDirectory = Path.GetDirectoryName(absolutePath) ?? _rootPath;

            Directory.CreateDirectory(targetDirectory);

            await using (var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            return new StoredFileInfo
            {
                FileName = fileName,
                OriginalFileName = file.FileName,
                RelativePath = relativePath.Replace('\\', '/'),
                AbsolutePath = absolutePath,
                FileExtension = normalizedExtension,
                MimeType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                FileSize = file.Length
            };
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            var absolutePath = ResolveAbsolutePath(relativePath);
            if (!File.Exists(absolutePath))
            {
                throw new NotFoundException("Fichier introuvable sur le stockage local.");
            }

            Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult(stream);
        }

        public bool Exists(string relativePath)
        {
            var absolutePath = ResolveAbsolutePath(relativePath);
            return File.Exists(absolutePath);
        }

        private string ResolveAbsolutePath(string relativePath)
        {
            var normalizedRelative = (relativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            var absolutePath = Path.GetFullPath(Path.Combine(_rootPath, normalizedRelative));
            var normalizedRoot = Path.GetFullPath(_rootPath);

            if (!absolutePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new ServiceException("Chemin de fichier invalide.");
            }

            return absolutePath;
        }

        private static string ResolveRootPath(string? configuredRoot)
        {
            var root = string.IsNullOrWhiteSpace(configuredRoot) ? "StorageFiles" : configuredRoot.Trim();
            return Path.IsPathRooted(root)
                ? root
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), root));
        }

        private static HashSet<string> ParseAllowedExtensions(string? configValue)
        {
            if (string.IsNullOrWhiteSpace(configValue))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return configValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ext => ext.StartsWith('.') ? ext.ToLowerInvariant() : $".{ext.ToLowerInvariant()}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string SanitizeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "default";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = value.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (invalidChars.Contains(chars[i]) || chars[i] == ' ')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }
    }
}
