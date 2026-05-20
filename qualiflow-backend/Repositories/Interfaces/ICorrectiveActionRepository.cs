using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface ICorrectiveActionRepository
    {
        Task<int> SyncOverdueStatusesAsync(int? organizationId = null, int? nonConformityId = null);
        Task<IEnumerable<CorrectiveActionData>> GetByNonConformityIdAsync(int nonConformityId);
        Task<IEnumerable<CorrectiveActionListItemData>> SearchAsync(
            int pageNumber,
            int pageSize,
            string? search,
            string? status,
            string? type,
            int? responsibleUserId,
            int? nonConformityId,
            bool? isOverdue,
            DateTime? fromDate,
            DateTime? toDate,
            int organizationId);

        Task<int> CountSearchAsync(
            string? search,
            string? status,
            string? type,
            int? responsibleUserId,
            int? nonConformityId,
            bool? isOverdue,
            DateTime? fromDate,
            DateTime? toDate,
            int organizationId);

        Task<CorrectiveActionDetailsData?> GetDetailsByIdAsync(int id, int organizationId);
        Task<IEnumerable<CorrectiveActionListItemData>> GetByNonConformityForListAsync(int nonConformityId, int organizationId);
        Task<IEnumerable<CorrectiveActionListItemData>> GetForStatisticsAsync(int organizationId);
        Task<CorrectiveAction?> GetByIdAsync(int id);
        Task<int> CreateAsync(CorrectiveAction action);
        Task<bool> UpdateAsync(CorrectiveAction action);
        Task<bool> DeleteAsync(int id, int organizationId);
        Task<bool> UpdateStatusAsync(int id, int organizationId, string status, DateTime? completionDate, DateTime updatedAt);
        Task<bool> UpdateEffectivenessAsync(int id, int organizationId, bool effectivenessVerified, string? effectivenessComment, DateTime updatedAt, string? status = null);
        Task<int> CountOverdueAsync(int organizationId);
    }
}
