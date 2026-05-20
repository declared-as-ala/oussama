using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IProcedureActionLogRepository
    {
        Task<int> CreateAsync(ProcedureActionLog actionLog);
        Task<IEnumerable<ProcedureActionLogData>> GetByProcedureIdAsync(int procedureId, int organizationId);
        Task<ProcedureActionLog?> GetByIdAsync(int logId, int organizationId);
        Task<bool> DeleteAsync(int logId, int organizationId);
    }
}
