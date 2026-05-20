using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface ICorrectiveActionActionLogRepository
    {
        Task<int> CreateAsync(CorrectiveActionActionLog actionLog);
        Task<IEnumerable<CorrectiveActionActionLogData>> GetByCorrectiveActionIdAsync(int correctiveActionId, int organizationId);
        Task<CorrectiveActionActionLog?> GetByIdAsync(int logId, int organizationId);
        Task<bool> DeleteAsync(int logId, int organizationId);
    }
}
