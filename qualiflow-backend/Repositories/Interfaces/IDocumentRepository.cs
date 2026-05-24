using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IDocumentRepository
    {
        Task<IEnumerable<DocumentListItemData>> SearchAsync(
            int pageNumber,
            int pageSize,
            string? search,
            string? type,
            string? status,
            int? processId,
            int? procedureId,
            int? ownerUserId,
            int? organizationId,
            bool pendingValidationOnly,
            bool hidePendingValidationFromGlobal,
            int? restrictedUserId = null);

        Task<int> CountSearchAsync(
            string? search,
            string? type,
            string? status,
            int? processId,
            int? procedureId,
            int? ownerUserId,
            int? organizationId,
            bool pendingValidationOnly,
            bool hidePendingValidationFromGlobal,
            int? restrictedUserId = null);

        Task<Document?> GetByIdAsync(int id);
        Task<Document?> GetByIdIncludingDeletedAsync(int id);
        Task<DocumentDetailsData?> GetDetailsByIdAsync(int id);
        Task<DocumentListItemData?> GetListItemByIdAsync(int id);
        Task<IEnumerable<DocumentListItemData>> GetDeletedAsync(int pageNumber, int pageSize, int organizationId);
        Task<int> CountDeletedAsync(int organizationId);
        Task<IEnumerable<Document>> GetByIdsAsync(int organizationId, IEnumerable<int> ids);
        Task<bool> ExistsCodeAsync(int organizationId, string code, int? excludeId = null);
        Task<int> CreateAsync(Document document);
        Task<bool> UpdateAsync(Document document);
        Task<bool> SoftDeleteAsync(int id, int organizationId);
        Task<bool> RestoreAsync(int id, int organizationId);
        Task<bool> PermanentDeleteAsync(int id, int organizationId);
        Task<int> PurgeExpiredDeletedAsync(int organizationId, DateTime cutoffUtc);
        Task<bool> SetActiveAsync(int id, bool isActive);
        Task<bool> SetCurrentVersionAsync(int documentId, int? currentVersionId);
        Task<IEnumerable<Document>> GetByOrganizationAsync(int? organizationId);
        Task<IEnumerable<DocumentExpiringData>> GetExpiringAsync(int organizationId, int withinDays);
        Task<IEnumerable<int>> GetProcessIdsByDocumentIdAsync(int documentId);
        Task<IEnumerable<int>> GetProcedureIdsByDocumentIdAsync(int documentId);
        Task<bool> AddProcessLinkAsync(int documentId, int processId);
        Task<bool> RemoveProcessLinkAsync(int documentId, int processId);
    }
}
