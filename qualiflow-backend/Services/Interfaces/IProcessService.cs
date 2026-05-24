using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Processes;

namespace DocApi.Services.Interfaces
{
    public interface IProcessService
    {
        Task<PagedProcessResponse> GetProcessesAsync(ProcessListQueryParameters query, UserContext userContext);
        Task<ProcessDetailsResponse> GetByIdAsync(int id, UserContext userContext);
        Task<ProcessResponse> CreateAsync(CreateProcessRequest request, UserContext userContext, int? organizationId = null);
        Task<ProcessResponse> UpdateAsync(int id, UpdateProcessRequest request, UserContext userContext);
        Task<bool> DeleteAsync(int id, UserContext userContext);
        Task<ProcessResponse> ToggleStatusAsync(int id, UserContext userContext);
        Task<ProcessResponse> UpdatePilotAsync(int id, UpdateProcessPilotRequest request, UserContext userContext);
        Task<List<ProcessActorResponse>> GetActorsAsync(int processId, UserContext userContext);
        Task<List<ProcessActorResponse>> AssignActorsAsync(int processId, AssignProcessActorsRequest request, UserContext userContext);
        Task<bool> RemoveActorAsync(int processId, int userId, UserContext userContext);
        Task<ProcessMapResponse> GetMapAsync(UserContext userContext);
        Task<ProcessStatisticsResponse> GetStatisticsAsync(UserContext userContext);
        Task<List<ProcessActionLogResponse>> GetActionLogsAsync(int processId, UserContext userContext);
        Task<bool> DeleteActionLogAsync(int logId, UserContext userContext);
        Task<bool> AddDocumentLinkAsync(int processId, int documentId, UserContext userContext);
        Task<bool> RemoveDocumentLinkAsync(int processId, int documentId, UserContext userContext);
    }
}
