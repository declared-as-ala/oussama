using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Services.Models;

namespace DocApi.Services.Interfaces
{
    public interface IOneSignalService
    {
        Task<OneSignalSendResult> SendToExternalIdsAsync(
            IReadOnlyCollection<string> externalIds,
            string title,
            string message,
            IDictionary<string, string>? data = null,
            CancellationToken cancellationToken = default);

        Task<OneSignalSendResult> SendByTagsAsync(
            IReadOnlyDictionary<string, string> tags,
            string title,
            string message,
            IDictionary<string, string>? data = null,
            CancellationToken cancellationToken = default);
    }
}
