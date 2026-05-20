using System.Threading;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Support;

namespace DocApi.Services.Interfaces
{
    public interface ISupportService
    {
        Task<SupportContactInfoResponse> GetContactInfoAsync(CancellationToken cancellationToken = default);

        Task<SubmitSupportTicketResponse> SubmitTicketAsync(
            SubmitSupportTicketRequest request,
            UserContext userContext,
            CancellationToken cancellationToken = default);
    }
}
