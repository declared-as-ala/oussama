using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IIndicatorRepository
    {
        Task<IEnumerable<IndicatorListItemData>> SearchAsync(
            int pageNumber,
            int pageSize,
            string? search,
            string? status,
            int? processId,
            string? measurementFrequency,
            int? responsibleUserId,
            bool? isInAlert,
            int organizationId);

        Task<int> CountSearchAsync(
            string? search,
            string? status,
            int? processId,
            string? measurementFrequency,
            int? responsibleUserId,
            bool? isInAlert,
            int organizationId);

        Task<Indicator?> GetByIdAsync(int id);
        Task<IndicatorDetailsData?> GetDetailsByIdAsync(int id, int organizationId);
        Task<IEnumerable<IndicatorListItemData>> GetByProcessAsync(int processId, int organizationId);
        Task<IEnumerable<IndicatorStatisticsData>> GetForStatisticsAsync(int organizationId);
        Task<bool> ExistsCodeAsync(int organizationId, string code, int? excludeId = null);
        Task<int> CreateAsync(Indicator indicator);
        Task<bool> UpdateAsync(Indicator indicator);
        Task<bool> DeleteAsync(int id, int organizationId);
        Task<bool> ToggleStatusAsync(int id, int organizationId, string status, DateTime updatedAt);
    }
}
