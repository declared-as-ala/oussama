using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DocApi.Infrastructure
{
    public interface IProfilePhotoStorageService
    {
        Task<string> SaveAsync(IFormFile file, int userId, CancellationToken cancellationToken = default);
        Task<(Stream Stream, string ContentType, string FileName)> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);
    }
}
