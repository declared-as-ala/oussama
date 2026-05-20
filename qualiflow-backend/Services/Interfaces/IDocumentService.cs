using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Documents;

namespace DocApi.Services.Interfaces
{
    public interface IDocumentService
    {
        Task<PagedDocumentResponse> GetDocumentsAsync(DocumentListQueryRequest query, UserContext userContext);
        Task<DocumentDetailsResponse> GetByIdAsync(int id, UserContext userContext);
        Task<DocumentResponse> CreateAsync(CreateDocumentRequest request, UserContext userContext);
        Task<DocumentResponse> UpdateAsync(int id, UpdateDocumentRequest request, UserContext userContext);
        Task<DocumentResponse> UpdateStatusAsync(int id, UpdateDocumentStatusRequest request, UserContext userContext);
        Task<bool> DeleteAsync(int id, UserContext userContext);
        Task<PagedDocumentResponse> GetTrashAsync(DocumentListQueryRequest query, UserContext userContext);
        Task<bool> RestoreAsync(int id, UserContext userContext);
        Task<bool> PermanentDeleteAsync(int id, UserContext userContext);
        Task<DocumentResponse> ToggleStatusAsync(int id, UserContext userContext);
        Task<List<DocumentActionLogResponse>> GetActionLogsAsync(int documentId, UserContext userContext);
        Task<List<DocumentExpiringResponse>> GetExpiringAsync(int withinDays, UserContext userContext);
        Task<DocumentStatisticsResponse> GetStatisticsAsync(UserContext userContext);

        Task<List<DocumentVersionResponse>> GetVersionsAsync(int documentId, UserContext userContext);
        Task<DocumentVersionResponse> GetVersionByIdAsync(int documentId, int versionId, UserContext userContext);
        Task<DocumentVersionResponse> CreateVersionAsync(int documentId, CreateDocumentVersionRequest request, UserContext userContext);
        Task<DocumentVersionResponse> UploadVersionAsync(int documentId, UploadDocumentVersionRequest request, UserContext userContext);
        Task<DocumentVersionResponse> UpdateVersionStatusAsync(int documentId, int versionId, UpdateDocumentVersionStatusRequest request, UserContext userContext);

        Task<(Stream Stream, string ContentType, string FileName)> DownloadCurrentAsync(int documentId, UserContext userContext);
        Task<(Stream Stream, string ContentType, string FileName)> DownloadVersionAsync(int documentId, int versionId, UserContext userContext);
        Task<(Stream Stream, string ContentType)> PreviewCurrentAsync(int documentId, UserContext userContext);
    }
}
