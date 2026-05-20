using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IDocumentVersionRepository
    {
        Task<DocumentVersion?> GetByIdAsync(int id);
        Task<DocumentVersion?> GetByDocumentAndVersionIdAsync(int documentId, int versionId);
        Task<DocumentVersionData?> GetDetailsByIdAsync(int id);
        Task<DocumentVersionData?> GetCurrentByDocumentIdAsync(int documentId);
        Task<IEnumerable<DocumentVersionData>> GetByDocumentIdAsync(int documentId);
        Task<byte[]?> GetFileContentAsync(int versionId);
        Task<bool> ExistsVersionNumberAsync(int documentId, string versionNumber, int? excludeId = null);
        Task<int> CreateAsync(DocumentVersion version);
        Task<bool> UpdateAsync(DocumentVersion version);
        Task<bool> UpdateStatusAsync(
            int versionId,
            string status,
            string? revisionComment,
            int? verifiedByUserId,
            System.DateTime? verifiedAt,
            int? validatedByUserId,
            System.DateTime? validatedAt,
            System.DateTime? updatedAt);
        Task<bool> SetCurrentVersionAsync(int documentId, int versionId);
    }
}
