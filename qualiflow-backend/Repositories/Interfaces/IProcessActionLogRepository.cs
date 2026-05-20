using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IProcessActionLogRepository
    {
        Task<int> CreateAsync(ProcessActionLog actionLog);
        Task<IEnumerable<ProcessActionLogData>> GetByProcessIdAsync(int processId, int organizationId);
        Task<ProcessActionLog?> GetByIdAsync(int logId, int organizationId);
        Task<bool> DeleteAsync(int logId, int organizationId);
    }
}
