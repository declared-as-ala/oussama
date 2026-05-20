using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Organizations;

namespace DocApi.Services.Interfaces
{
    public interface IOrganizationService
    {
        Task<OrganizationResponse> GetByIdAsync(int id);
        Task<PagedOrganizationsResponse> GetAllAsync(OrganizationListQueryParameters query);
        Task<int> CreateAsync(CreateOrganizationRequest request);
        Task<bool> UpdateAsync(int id, UpdateOrganizationRequest request, bool allowSubscriptionUpdate = true);
        Task<OrganizationResponse> ToggleStatusAsync(int id, ToggleOrganizationStatusRequest request);

        Task<OrganizationResponse> GetMyOrganizationAsync(UserContext userContext);
        Task<bool> UpdateMyOrganizationAsync(UpdateOrganizationRequest request, UserContext userContext);
        Task<OrganizationLogoResponse> UploadMyLogoAsync(UploadOrganizationLogoRequest request, UserContext userContext);
        Task<(Stream Stream, string ContentType, string FileName)> GetOrganizationLogoAsync(int id, UserContext userContext);
        Task<(Stream Stream, string ContentType, string FileName)> GetMyOrganizationLogoAsync(UserContext userContext);
        Task<bool> DeleteAsync(int id);
    }
}
