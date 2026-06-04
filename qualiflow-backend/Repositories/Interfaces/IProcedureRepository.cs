using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IProcedureRepository
    {
        Task<IEnumerable<ProcedureListItemData>> SearchAsync(
            int pageNumber,
            int pageSize,
            string? search,
            int? processId,
            string? status,
            int? responsibleUserId,
            int? organizationId,
            int? restrictedUserId = null);

        Task<int> CountSearchAsync(
            string? search,
            int? processId,
            string? status,
            int? responsibleUserId,
            int? organizationId,
            int? restrictedUserId = null);

        Task<Procedure?> GetByIdAsync(int id);
        Task<ProcedureListItemData?> GetListItemByIdAsync(int id);
        Task<IEnumerable<ProcedureListItemData>> GetByProcessIdAsync(int processId, int? organizationId);
        Task<bool> ExistsCodeAsync(int organizationId, string code, int? excludeId = null);
        Task<int> CreateAsync(Procedure procedure, IEnumerable<int>? additionalProcessIds = null);
        Task<bool> UpdateAsync(Procedure procedure);
        Task<bool> DeleteAsync(int id);
        Task<bool> ToggleStatusAsync(int id, string status);
        Task<IEnumerable<Procedure>> GetByOrganizationAsync(int? organizationId, int? restrictedUserId = null);
        Task<bool> AddProcessLinkAsync(int processId, int procedureId);
        Task<bool> RemoveProcessLinkAsync(int processId, int procedureId);
        Task<bool> ClearProcessLinksAsync(int procedureId);
        Task<bool> AddDocumentLinkAsync(int procedureId, int documentId);
        Task<bool> RemoveDocumentLinkAsync(int procedureId, int documentId);
        Task<IEnumerable<int>> GetLinkedDocumentIdsAsync(int procedureId);
    }
}
