using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DocApi.Infrastructure
{
    public interface IFileStorageService
    {
        Task<StoredFileInfo> SaveAsync(
            IFormFile file,
            string organizationSegment,
            string documentCode,
            string versionNumber,
            CancellationToken cancellationToken = default);

        Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);
        bool Exists(string relativePath);
    }
}
