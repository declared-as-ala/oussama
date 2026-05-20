using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IIndicatorAlertRepository
    {
        Task<int> CreateAsync(IndicatorAlert alert);
        Task<bool> ExistsOpenForValueAsync(int indicatorId, int indicatorValueId, int organizationId);
        Task<int> ResolveOpenByIndicatorAsync(int indicatorId, int organizationId, DateTime resolvedAt);
        Task<IEnumerable<IndicatorAlertData>> GetActiveAsync(int organizationId);
        Task<IEnumerable<IndicatorAlertData>> GetByIndicatorIdAsync(int indicatorId, int organizationId, bool? isResolved = null, int? limit = null);
    }
}
