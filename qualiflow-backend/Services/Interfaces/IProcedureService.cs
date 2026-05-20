using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Procedures;

namespace DocApi.Services.Interfaces
{
    public interface IProcedureService
    {
        Task<PagedProcedureResponse> GetProceduresAsync(ProcedureListQueryParameters query, UserContext userContext);
        Task<ProcedureDetailsResponse> GetByIdAsync(int id, UserContext userContext);
        Task<List<ProcedureListItemResponse>> GetByProcessIdAsync(int processId, UserContext userContext);
        Task<ProcedureResponse> CreateAsync(CreateProcedureRequest request, UserContext userContext);
        Task<ProcedureResponse> UpdateAsync(int id, UpdateProcedureRequest request, UserContext userContext);
        Task<bool> DeleteAsync(int id, UserContext userContext);
        Task<ProcedureResponse> ToggleStatusAsync(int id, UserContext userContext);
        Task<ProcedureStatisticsResponse> GetStatisticsAsync(UserContext userContext);
        Task<List<InstructionResponse>> GetInstructionsAsync(int procedureId, UserContext userContext);
        Task<InstructionResponse> CreateInstructionAsync(int procedureId, CreateInstructionRequest request, UserContext userContext);
        Task<InstructionResponse> UpdateInstructionAsync(int procedureId, int instructionId, UpdateInstructionRequest request, UserContext userContext);
        Task<bool> DeleteInstructionAsync(int procedureId, int instructionId, UserContext userContext);
        Task<List<ProcedureActionLogResponse>> GetActionLogsAsync(int procedureId, UserContext userContext);
        Task<bool> DeleteActionLogAsync(int logId, UserContext userContext);
        Task<bool> AddProcessLinkAsync(int processId, int procedureId, UserContext userContext);
        Task<bool> RemoveProcessLinkAsync(int processId, int procedureId, UserContext userContext);
    }
}
