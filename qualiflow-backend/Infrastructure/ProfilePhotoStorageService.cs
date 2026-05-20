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
    public class ProfilePhotoStorageService : IProfilePhotoStorageService
    {
        private readonly string _rootPath;
        private readonly long _maxFileSizeBytes;
        private readonly HashSet<string> _allowedExtensions;

        public ProfilePhotoStorageService(IConfiguration configuration)
        {
            var configuredPath = configuration["Storage:ProfilePhotosPath"];
            _rootPath = ResolveRootPath(configuredPath);
            var maxMb = configuration.GetValue("Storage:ProfilePhotoMaxFileSizeMb", 3);
            _maxFileSizeBytes = Math.Max(maxMb, 1) * 1024L * 1024L;

            _allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg"
            };

            Directory.CreateDirectory(_rootPath);
        }

        public async Task<string> SaveAsync(IFormFile file, int userId, CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length <= 0)
            {
                throw new ServiceException("La photo de profil est obligatoire.");
            }

            if (file.Length > _maxFileSizeBytes)
            {
                throw new ServiceException($"La photo depasse la taille maximale ({_maxFileSizeBytes / 1024 / 1024} MB).");
            }

            var extension = Path.GetExtension(file.FileName)?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || !_allowedExtensions.Contains(extension))
            {
                throw new ServiceException("Format photo non autorise. Utilise .png, .jpg ou .jpeg.");
            }

            var fileName = $"user-{userId}{extension}";
            var absolutePath = Path.Combine(_rootPath, fileName);

            foreach (var existingPath in Directory.GetFiles(_rootPath, $"user-{userId}.*"))
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

            return Path.Combine("profile-photos", fileName).Replace('\\', '/');
        }

        public Task<(Stream Stream, string ContentType, string FileName)> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new NotFoundException("Photo de profil introuvable.");
            }

            var fileName = Path.GetFileName(relativePath);
            var absolutePath = Path.Combine(_rootPath, fileName);
            if (!File.Exists(absolutePath))
            {
                throw new NotFoundException("Photo de profil introuvable.");
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
                ? Path.Combine("StorageFiles", "profile-photos")
                : configuredPath.Trim();

            return Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }
    }
}
