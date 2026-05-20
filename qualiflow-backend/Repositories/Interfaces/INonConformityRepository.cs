using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface INonConformityRepository
    {
        Task<IEnumerable<NonConformityListItemData>> SearchAsync(
            int pageNumber,
            int pageSize,
            string? search,
            string? status,
            string? severity,
            int? processId,
            int? responsibleUserId,
            int? organizationId);

        Task<int> CountSearchAsync(
            string? search,
            string? status,
            string? severity,
            int? processId,
            int? responsibleUserId,
            int? organizationId);

        Task<NonConformity?> GetByIdAsync(int id);
        Task<NonConformityListItemData?> GetListItemByIdAsync(int id);
        Task<bool> ExistsCodeAsync(int organizationId, string code, int? excludeId = null);
        Task<string> GenerateNextCodeAsync(int organizationId);
        Task<int> CreateAsync(NonConformity nonConformity);
        Task<bool> UpdateAsync(NonConformity nonConformity);
        Task<bool> DeleteAsync(int id);
        Task<bool> UpdateStatusAsync(int id, string status);
        Task<bool> ValidateAsync(int id, string code, int responsibleUserId, string status);
        Task<IEnumerable<NonConformity>> GetByOrganizationAsync(int? organizationId);
        Task<IEnumerable<NonConformityListItemData>> GetAwaitingValidationAsync(int organizationId, int pageNumber, int pageSize);
        Task<int> CountAwaitingValidationAsync(int organizationId);

        Task<int> AddAttachmentAsync(NonConformityAttachment attachment);
        Task<NonConformityAttachment?> GetAttachmentByIdAsync(int attachmentId);
        Task<IEnumerable<NonConformityAttachment>> GetAttachmentsByNonConformityIdAsync(int nonConformityId);
        Task<bool> DeleteAttachmentAsync(int attachmentId);
    }
}
