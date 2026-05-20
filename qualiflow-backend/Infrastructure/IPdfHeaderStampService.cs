using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DocApi.Infrastructure
{
    public interface IPdfHeaderStampService
    {
        Task<Stream> AddHeaderAsync(Stream sourcePdfStream, PdfHeaderMetadata metadata, CancellationToken cancellationToken = default);
        Task<Stream> CreatePdfFromTextAsync(string textContent, PdfHeaderMetadata metadata, CancellationToken cancellationToken = default);
    }
}
