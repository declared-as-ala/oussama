using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IIndicatorValueRepository
    {
        Task<IEnumerable<IndicatorValueData>> GetByIndicatorIdAsync(int indicatorId, int organizationId, int? take = null);
        Task<IndicatorValueData?> GetByIdAsync(int valueId);
        Task<IndicatorValueData?> GetLatestByIndicatorIdAsync(int indicatorId, int organizationId);
        Task<bool> ExistsPeriodAsync(int indicatorId, int organizationId, string periodLabel, int? excludeValueId = null);
        Task<int> CreateAsync(IndicatorValue value);
        Task<bool> UpdateAsync(IndicatorValue value);
        Task<bool> DeleteAsync(int valueId, int indicatorId, int organizationId);
    }
}
