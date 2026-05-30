using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IIndicatorActionLogRepository
    {
        Task<int> CreateAsync(IndicatorActionLog actionLog);
        Task<IEnumerable<IndicatorActionLogData>> GetByIndicatorIdAsync(int indicatorId, int organizationId);
        Task<IndicatorActionLog?> GetByIdAsync(int logId, int organizationId);
        Task<bool> DeleteAsync(int logId, int organizationId);
    }
}
