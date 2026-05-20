using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IProcessRepository
    {
        Task<IEnumerable<Process>> SearchAsync(
            int pageNumber,
            int pageSize,
            string? search,
            string? type,
            string? status,
            int? pilotUserId,
            int? organizationId,
            int? restrictedUserId = null);

        Task<int> CountSearchAsync(
            string? search,
            string? type,
            string? status,
            int? pilotUserId,
            int? organizationId,
            int? restrictedUserId = null);

        Task<Process?> GetByIdAsync(int id);
        Task<bool> ExistsCodeAsync(int organizationId, string code, int? excludeId = null);
        Task<int> CreateAsync(Process process);
        Task<bool> UpdateAsync(Process process);
        Task<bool> DeleteAsync(int id);
        Task<bool> ToggleStatusAsync(int id, string status);
        Task<IEnumerable<Process>> GetByOrganizationAsync(int? organizationId, int? restrictedUserId = null);
        Task<IEnumerable<Process>> GetByProcedureIdAsync(int procedureId, int? organizationId = null);
    }
}
