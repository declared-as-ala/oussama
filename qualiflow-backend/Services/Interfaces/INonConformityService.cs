using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.NonConformities;

namespace DocApi.Services.Interfaces
{
    public interface INonConformityService
    {
        Task<PagedNonConformityResponse> GetNonConformitiesAsync(NonConformityListQueryParameters query, UserContext userContext);
        Task<PagedNonConformityResponse> GetAwaitingValidationAsync(NonConformityListQueryParameters query, UserContext userContext);
        Task<NonConformityDetailsResponse> GetByIdAsync(int id, UserContext userContext);
        Task<NonConformityResponse> CreateAsync(CreateNonConformityRequest request, UserContext userContext);
        Task<NonConformityResponse> UpdateAsync(int id, UpdateNonConformityRequest request, UserContext userContext);
        Task<bool> DeleteAsync(int id, UserContext userContext);
        Task<NonConformityResponse> UpdateStatusAsync(int id, UpdateNonConformityStatusRequest request, UserContext userContext);
        Task<NonConformityResponse> ValidateAsync(int id, ValidateNonConformityRequest request, UserContext userContext);
        Task<NonConformityStatisticsResponse> GetStatisticsAsync(UserContext userContext);

        Task<NonConformityAttachmentResponse> AddAttachmentAsync(int nonConformityId, string originalFileName, string mimeType, byte[] content, UserContext userContext);
        Task<NonConformityAttachment?> GetAttachmentContentAsync(int attachmentId, UserContext userContext);
        Task<bool> DeleteAttachmentAsync(int attachmentId, UserContext userContext);
    }
}
