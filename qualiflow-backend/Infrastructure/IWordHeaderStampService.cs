using System.Threading;
using System.Threading.Tasks;

namespace DocApi.Infrastructure
{
    public interface IWordHeaderStampService
    {
        Task ApplyFirstPageHeaderAsync(string absoluteDocxPath, PdfHeaderMetadata metadata, CancellationToken cancellationToken = default);
    }
}

