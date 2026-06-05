using System.IO;
using System.Threading.Tasks;

namespace DocApi.Services.Interfaces
{
    public interface IConversionService
    {
        Task<(Stream Stream, string ContentType, string FileName)> ConvertAsync(Stream sourceStream, string sourceContentType, string sourceFileName, string targetFormat);
    }
}
