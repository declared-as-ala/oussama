using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.CorrectiveActions;

namespace DocApi.Services.Interfaces
{
    public interface ICorrectiveActionService
    {
        Task<PagedCorrectiveActionResponse> GetCorrectiveActionsAsync(GetCorrectiveActionsQueryRequest query, UserContext userContext);
        Task<CorrectiveActionDetailsResponse> GetByIdAsync(int id, UserContext userContext);
        Task<CorrectiveActionResponse> CreateAsync(CreateCorrectiveActionRequest request, UserContext userContext);
        Task<CorrectiveActionResponse> UpdateAsync(int id, UpdateCorrectiveActionRequest request, UserContext userContext);
        Task<bool> DeleteAsync(int id, UserContext userContext);
        Task<CorrectiveActionResponse> UpdateStatusAsync(int id, UpdateCorrectiveActionStatusRequest request, UserContext userContext);
        Task<CorrectiveActionResponse> VerifyEffectivenessAsync(int id, VerifyCorrectiveActionEffectivenessRequest request, UserContext userContext);
        Task<CorrectiveActionStatisticsResponse> GetStatisticsAsync(UserContext userContext);
        Task<List<CorrectiveActionListItemResponse>> GetByNonConformityIdAsync(int nonConformityId, UserContext userContext);
        Task<List<CorrectiveActionActionLogResponse>> GetHistoryAsync(int id, UserContext userContext);
        Task<bool> DeleteActionLogAsync(int logId, UserContext userContext);
    }
}
