using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IAlertRuleRepository
    {
        Task<IEnumerable<AlertRule>> GetAllAsync(int? organizationId);
        Task<AlertRule?> GetByIdAsync(int id);
        Task<bool> ExistsCodeAsync(int organizationId, string code, int? excludeId = null);
        Task<int> CreateAsync(AlertRule alertRule);
        Task<bool> UpdateAsync(AlertRule alertRule);
        Task<bool> ToggleStatusAsync(int id, bool isActive);
    }
}
