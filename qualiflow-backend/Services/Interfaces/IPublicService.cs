using System.Threading;
using System.Threading.Tasks;
using DocApi.DTOs.Public;

namespace DocApi.Services.Interfaces
{
    public interface IPublicService
    {
        Task<SubmitOrganizationRequestResponse> SendVerificationCodeAsync(string email, CancellationToken cancellationToken = default);
        Task<SubmitOrganizationRequestResponse> VerifyCodeAsync(string email, string code, CancellationToken cancellationToken = default);
        Task<SubmitOrganizationRequestResponse> SubmitOrganizationRequestAsync(SubmitOrganizationRequest request, CancellationToken cancellationToken = default);
    }
}
