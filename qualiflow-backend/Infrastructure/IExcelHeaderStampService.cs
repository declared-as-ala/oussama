using System.Threading;
using System.Threading.Tasks;

namespace DocApi.Infrastructure
{
    public interface IExcelHeaderStampService
    {
        Task ApplyWorkbookHeaderAsync(string absoluteXlsxPath, PdfHeaderMetadata metadata, CancellationToken cancellationToken = default);
    }
}
