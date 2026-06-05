using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.Domain.Enums;
using DocApi.DTOs.Documents;
using DocApi.DTOs.Notifications;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DocApi.Services
{
    public class DocumentService : IDocumentService
    {
        private const string UserVisibleStatusesFilter = "__APPROVED_OR_PUBLISHED__";
        private const int TrashRetentionDays = 30;
        private readonly IDocumentRepository _documentRepository;
        private readonly IDocumentVersionRepository _documentVersionRepository;
        private readonly IDocumentActionLogRepository _documentActionLogRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProcessRepository _processRepository;
        private readonly IProcedureRepository _procedureRepository;
        private readonly IUserRepository _userRepository;
        private readonly IProcessActorRepository _processActorRepository;
        private readonly IProcedureActionLogRepository _procedureActionLogRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IPdfHeaderStampService _pdfHeaderStampService;
        private readonly IWordHeaderStampService _wordHeaderStampService;
        private readonly IExcelHeaderStampService _excelHeaderStampService;
        private readonly INotificationEventPublisher _notificationEventPublisher;
        private readonly IActionLogger _actionLogger;
        private static readonly HashSet<string> SupportedDocumentFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".docx",
            ".xlsx",
            ".txt"
        };
        private readonly HashSet<string> _allowedDocumentExtensions;
        private readonly long _maxDocumentFileSizeBytes;

        public DocumentService(
            IDocumentRepository documentRepository,
            IDocumentVersionRepository documentVersionRepository,
            IDocumentActionLogRepository documentActionLogRepository,
            IOrganizationRepository organizationRepository,
            IProcessRepository processRepository,
            IProcedureRepository procedureRepository,
            IUserRepository userRepository,
            IProcessActorRepository processActorRepository,
            IProcedureActionLogRepository procedureActionLogRepository,
            IFileStorageService fileStorageService,
            IPdfHeaderStampService pdfHeaderStampService,
            IWordHeaderStampService wordHeaderStampService,
            IExcelHeaderStampService excelHeaderStampService,
            INotificationEventPublisher notificationEventPublisher,
            IActionLogger actionLogger,
            IConfiguration configuration)
        {
            _documentRepository = documentRepository;
            _documentVersionRepository = documentVersionRepository;
            _documentActionLogRepository = documentActionLogRepository;
            _organizationRepository = organizationRepository;
            _processRepository = processRepository;
            _procedureRepository = procedureRepository;
            _userRepository = userRepository;
            _processActorRepository = processActorRepository;
            _procedureActionLogRepository = procedureActionLogRepository;
            _fileStorageService = fileStorageService;
            _pdfHeaderStampService = pdfHeaderStampService;
            _wordHeaderStampService = wordHeaderStampService;
            _excelHeaderStampService = excelHeaderStampService;
            _notificationEventPublisher = notificationEventPublisher;
            _actionLogger = actionLogger;
            _allowedDocumentExtensions = ParseAllowedExtensions(configuration["Storage:AllowedExtensions"]);
            var maxMb = configuration.GetValue("Storage:MaxFileSizeMb", 20);
            _maxDocumentFileSizeBytes = Math.Max(maxMb, 1) * 1024L * 1024L;
        }
        public async Task<PagedDocumentResponse> GetDocumentsAsync(DocumentListQueryRequest query, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
            var organizationScope = ResolveOrganizationScopeForRead(userContext, query.OrganizationId);
            var pendingValidationOnly = userContext.Role == UserRoles.RESPONSABLE_QUALITE && query.PendingValidationOnly;
            var hidePendingValidationFromGlobal = false;

            var statusFilter = ResolveReadableStatusFilter(query.Status, userContext);

            int? restrictedUserId = (userContext.Role == UserRoles.UTILISATEUR || userContext.Role == UserRoles.CHEF_SERVICE)
                ? userContext.UserId
                : null;

            var items = await _documentRepository.SearchAsync(
                pageNumber,
                pageSize,
                NormalizeSearch(query.Search),
                NormalizeUpper(query.Type),
                statusFilter,
                query.ProcessId,
                query.ProcedureId,
                query.OwnerUserId,
                organizationScope,
                pendingValidationOnly,
                hidePendingValidationFromGlobal,
                restrictedUserId);

            var total = await _documentRepository.CountSearchAsync(
                NormalizeSearch(query.Search),
                NormalizeUpper(query.Type),
                statusFilter,
                query.ProcessId,
                query.ProcedureId,
                query.OwnerUserId,
                organizationScope,
                pendingValidationOnly,
                hidePendingValidationFromGlobal,
                restrictedUserId);

            return new PagedDocumentResponse
            {
                Total = total,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Items = (await Task.Run(async () => {
                    var documentIds = items.Select(i => i.Id).ToList();
                    var processLookup = await _documentRepository.GetProcessIdsForDocumentsAsync(documentIds);
                    var procedureLookup = await _documentRepository.GetProcedureIdsForDocumentsAsync(documentIds);
                    
                    return items.Select(item => {
                        var response = MapToListItemResponse(item);
                        var procIds = processLookup[item.Id].ToList();
                        var prodIds = procedureLookup[item.Id].ToList();
                        
                        if (item.ProcessId.HasValue && !procIds.Contains(item.ProcessId.Value))
                        {
                            procIds.Add(item.ProcessId.Value);
                        }
                        if (item.ProcedureId.HasValue && !prodIds.Contains(item.ProcedureId.Value))
                        {
                            prodIds.Add(item.ProcedureId.Value);
                        }
                        
                        response.ProcessIds = procIds;
                        response.ProcedureIds = prodIds;
                        return response;
                    }).ToList();
                }))
            };
        }

        public async Task<DocumentDetailsResponse> GetByIdAsync(int id, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var document = await GetDocumentOrThrowAsync(id);
            await EnsureDocumentReadAccessAsync(document, userContext);

            var details = await _documentRepository.GetDetailsByIdAsync(id);
            if (details == null)
            {
                throw new NotFoundException("Document introuvable.");
            }

            var versionsData = (await _documentVersionRepository.GetByDocumentIdAsync(id)).ToList();
            var versions = versionsData.Select(MapToVersionResponse).ToList();

            DocumentVersionResponse? currentVersion = versions.FirstOrDefault(v => v.IsCurrent);
            if (currentVersion == null && document.CurrentVersionId.HasValue)
            {
                currentVersion = versions.FirstOrDefault(v => v.Id == document.CurrentVersionId.Value);
            }

            var status = currentVersion?.Status ?? DocumentConstants.StatusBrouillon;

            var processIds = (await _documentRepository.GetProcessIdsByDocumentIdAsync(id)).ToList();
            var procedureIds = (await _documentRepository.GetProcedureIdsByDocumentIdAsync(id)).ToList();

            return new DocumentDetailsResponse
            {
                Document = MapToDocumentResponse(details, currentVersion, processIds, procedureIds),
                CurrentVersion = currentVersion,
                Versions = versions,
            };
        }

        public async Task<DocumentResponse> CreateAsync(CreateDocumentRequest request, UserContext userContext)
        {
            EnsureCanSubmit(userContext);

            var primaryProcessId = request.ProcessIds?.FirstOrDefault() ?? request.ProcessId;
            var primaryProcedureId = request.ProcedureIds?.FirstOrDefault() ?? request.ProcedureId;

            await VerifyDocumentWritePermissionAsync(primaryProcessId, userContext);

            var organizationId = ResolveOrganizationScopeForWrite(userContext);

            var isPrivilegedCreator = userContext.Role == UserRoles.ADMIN_ORG
                                   || userContext.Role == UserRoles.RESPONSABLE_QUALITE
                                   || userContext.IsSuperAdmin;

            if (!isPrivilegedCreator)
            {
                var userProcesses = await _processRepository.GetByOrganizationAsync(organizationId, userContext.UserId);
                if (userProcesses == null || !userProcesses.Any())
                {
                    throw new ForbiddenException("Vous devez être affecté à au moins un processus pour déposer un document.");
                }
            }

            await ValidateDocumentPayloadAsync(
                primaryProcessId,
                primaryProcedureId,
                request.Code,
                request.Title,
                request.Type,
                request.OwnerUserId,
                organizationId,
                null);

            var document = new Document
            {
                OrganizationId = organizationId,
                ProcessId = primaryProcessId,
                ProcedureId = primaryProcedureId,
                ProcessIds = request.ProcessIds ?? new List<int>(),
                ProcedureIds = request.ProcedureIds ?? new List<int>(),
                Code = request.Code.Trim(),
                Title = request.Title.Trim(),
                Type = request.Type.Trim().ToUpperInvariant(),
                Description = NormalizeNullable(request.Description),
                Category = NormalizeNullable(request.Category),
                Keywords = NormalizeNullable(request.Keywords),
                Signature = request.Signature,
                OwnerUserId = request.OwnerUserId,
                CurrentVersionId = null,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            int id;
            try
            {
                id = await _documentRepository.CreateAsync(document);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation &&
                                               (string.Equals(ex.ConstraintName, "uq_documents_org_code", StringComparison.OrdinalIgnoreCase) ||
                                                string.Equals(ex.ConstraintName, "uq_documents_org_code_active", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ServiceException("Ce code document existe deja dans l'organisation.");
            }
            var created = await GetDocumentOrThrowAsync(id);
            var details = await _documentRepository.GetDetailsByIdAsync(id);
            if (details == null)
            {
                throw new ServiceException("Document cree mais introuvable.");
            }
            await LogDocumentActionAsync(
                created,
                null,
                "DOCUMENT_CREATED",
                null,
                created.Code,
                "Document cree.",
                userContext.UserId);

            await _actionLogger.LogActionAsync(
                organizationId,
                userContext.UserId,
                $"{userContext.FirstName} {userContext.LastName}",
                "DOCUMENT",
                "CREATE",
                $"Nouveau document : {created.Code}",
                $"Le document '{created.Title}' a été créé.");

            var procId = request.ProcedureIds?.FirstOrDefault() ?? request.ProcedureId;
            if (procId.HasValue)
            {
                await _procedureActionLogRepository.CreateAsync(new ProcedureActionLog
                {
                    OrganizationId = organizationId,
                    ProcedureId = procId.Value,
                    ActionType = "DOCUMENT_ADDED",
                    OldValue = null,
                    NewValue = created.Code,
                    Comment = $"Document ajouté : '{created.Title}'.",
                    PerformedByUserId = userContext.UserId,
                    PerformedAt = DateTime.UtcNow
                });
            }

            return MapToDocumentResponse(details, null, request.ProcessIds, request.ProcedureIds);
        }

        public async Task<DocumentResponse> UpdateAsync(int id, UpdateDocumentRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var document = await GetDocumentOrThrowAsync(id);
            EnsureDocumentWriteAccess(userContext, document.OrganizationId);

            var primaryProcessId = request.ProcessIds?.FirstOrDefault() ?? request.ProcessId;
            var primaryProcedureId = request.ProcedureIds?.FirstOrDefault() ?? request.ProcedureId;

            await VerifyDocumentWritePermissionAsync(primaryProcessId, userContext);

            await ValidateDocumentPayloadAsync(
                primaryProcessId,
                primaryProcedureId,
                request.Code,
                request.Title,
                request.Type,
                request.OwnerUserId,
                document.OrganizationId,
                id);

            document.ProcessId = primaryProcessId;
            document.ProcedureId = primaryProcedureId;
            document.ProcessIds = request.ProcessIds ?? new List<int>();
            document.ProcedureIds = request.ProcedureIds ?? new List<int>();
            document.Code = request.Code.Trim();
            document.Title = request.Title.Trim();
            document.Type = request.Type.Trim().ToUpperInvariant();
            document.Description = NormalizeNullable(request.Description);
            document.Category = NormalizeNullable(request.Category);
            document.Keywords = NormalizeNullable(request.Keywords);
            document.Signature = request.Signature;
            document.OwnerUserId = request.OwnerUserId;
            document.IsActive = request.IsActive;
            document.UpdatedAt = DateTime.UtcNow;

            await _documentRepository.UpdateAsync(document);
            await LogDocumentActionAsync(
                document,
                null,
                "DOCUMENT_UPDATED",
                null,
                document.Code,
                "Metadonnees du document modifiees.",
                userContext.UserId);

            var details = await _documentRepository.GetDetailsByIdAsync(id);
            if (details == null)
            {
                throw new NotFoundException("Document introuvable.");
            }

            DocumentVersionResponse? currentVersion = null;
            if (details.CurrentVersionId.HasValue)
            {
                var currentVersionData = await _documentVersionRepository.GetDetailsByIdAsync(details.CurrentVersionId.Value);
                currentVersion = currentVersionData == null ? null : MapToVersionResponse(currentVersionData);
            }

            return MapToDocumentResponse(details, currentVersion, request.ProcessIds, request.ProcedureIds);
        }

        public async Task<bool> DeleteAsync(int id, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var document = await GetDocumentOrThrowAsync(id);
            EnsureDocumentWriteAccess(userContext, document.OrganizationId);
            await VerifyDocumentWritePermissionAsync(document.ProcessId, userContext);

            var deleted = await _documentRepository.SoftDeleteAsync(id, document.OrganizationId);
            if (deleted)
            {
                await LogDocumentActionAsync(
                    document,
                    null,
                    "DOCUMENT_DELETED",
                    "ACTIVE",
                    "DELETED",
                    "Document place dans la corbeille.",
                    userContext.UserId);
            }

            return deleted;
        }

        public async Task<PagedDocumentResponse> GetTrashAsync(DocumentListQueryRequest query, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var organizationId = ResolveOrganizationScopeForWrite(userContext);
            await _documentRepository.PurgeExpiredDeletedAsync(organizationId, DateTime.UtcNow.AddDays(-TrashRetentionDays));

            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
            var items = await _documentRepository.GetDeletedAsync(pageNumber, pageSize, organizationId);
            var total = await _documentRepository.CountDeletedAsync(organizationId);

            return new PagedDocumentResponse
            {
                Total = total,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Items = items.Select(MapToListItemResponse).ToList()
            };
        }

        public async Task<bool> RestoreAsync(int id, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var document = await _documentRepository.GetByIdIncludingDeletedAsync(id);
            if (document == null || document.DeletedAt == null)
            {
                throw new NotFoundException("Document introuvable dans la corbeille.");
            }

            EnsureDocumentWriteAccess(userContext, document.OrganizationId);

            if (document.DeletedAt.Value <= DateTime.UtcNow.AddDays(-TrashRetentionDays))
            {
                await _documentRepository.PermanentDeleteAsync(id, document.OrganizationId);
                throw new NotFoundException("Le document a depasse les 30 jours de retention.");
            }

            var restored = await _documentRepository.RestoreAsync(id, document.OrganizationId);
            if (restored)
            {
                await LogDocumentActionAsync(
                    document,
                    null,
                    "DOCUMENT_RESTORED",
                    "DELETED",
                    "ACTIVE",
                    "Document restaure depuis la corbeille.",
                    userContext.UserId);
            }

            return restored;
        }

        public async Task<bool> PermanentDeleteAsync(int id, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var document = await _documentRepository.GetByIdIncludingDeletedAsync(id);
            if (document == null || document.DeletedAt == null)
            {
                throw new NotFoundException("Document introuvable dans la corbeille.");
            }

            EnsureDocumentWriteAccess(userContext, document.OrganizationId);

            await LogDocumentActionAsync(
                document,
                null,
                "DOCUMENT_PERMANENTLY_DELETED",
                "DELETED",
                "PERMANENTLY_DELETED",
                "Document supprime definitivement.",
                userContext.UserId);

            return await _documentRepository.PermanentDeleteAsync(id, document.OrganizationId);
        }

        public async Task<DocumentResponse> UpdateStatusAsync(int id, UpdateDocumentStatusRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var document = await GetDocumentOrThrowAsync(id);
            EnsureDocumentWriteAccess(userContext, document.OrganizationId);

            var targetVersionId = document.CurrentVersionId;
            if (!targetVersionId.HasValue)
            {
                var latestVersion = (await _documentVersionRepository.GetByDocumentIdAsync(id)).FirstOrDefault();
                if (latestVersion == null)
                {
                    throw new ServiceException("Aucune version n'est disponible pour ce document.");
                }

                targetVersionId = latestVersion.Id;
            }

            await UpdateVersionStatusAsync(
                id,
                targetVersionId.Value,
                new UpdateDocumentVersionStatusRequest
                {
                    Status = request.Status,
                    RevisionComment = request.RevisionComment
                },
                userContext);

            var details = await _documentRepository.GetDetailsByIdAsync(id);
            if (details == null)
            {
                throw new NotFoundException("Document introuvable.");
            }

            var updatedCurrent = await _documentVersionRepository.GetCurrentByDocumentIdAsync(id);
            var currentVersion = updatedCurrent == null ? null : MapToVersionResponse(updatedCurrent);

            var processIds = (await _documentRepository.GetProcessIdsByDocumentIdAsync(id)).ToList();
            var procedureIds = (await _documentRepository.GetProcedureIdsByDocumentIdAsync(id)).ToList();
            return MapToDocumentResponse(details, currentVersion, processIds, procedureIds);
        }

        public async Task<DocumentResponse> ToggleStatusAsync(int id, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var document = await GetDocumentOrThrowAsync(id);
            EnsureDocumentWriteAccess(userContext, document.OrganizationId);
            await VerifyDocumentWritePermissionAsync(document.ProcessId, userContext);

            var nextStatus = !document.IsActive;
            await _documentRepository.SetActiveAsync(id, nextStatus);
            await LogDocumentActionAsync(
                document,
                null,
                nextStatus ? "DOCUMENT_ACTIVATED" : "DOCUMENT_DEACTIVATED",
                document.IsActive ? "ACTIVE" : "INACTIVE",
                nextStatus ? "ACTIVE" : "INACTIVE",
                nextStatus ? "Document active." : "Document desactive.",
                userContext.UserId);
            document.IsActive = nextStatus;
            document.UpdatedAt = DateTime.UtcNow;

            var details = await _documentRepository.GetDetailsByIdAsync(id);
            if (details == null)
            {
                throw new NotFoundException("Document introuvable.");
            }

            DocumentVersionResponse? currentVersion = null;
            if (details.CurrentVersionId.HasValue)
            {
                var currentVersionData = await _documentVersionRepository.GetDetailsByIdAsync(details.CurrentVersionId.Value);
                currentVersion = currentVersionData == null ? null : MapToVersionResponse(currentVersionData);
            }

            var processIds = (await _documentRepository.GetProcessIdsByDocumentIdAsync(id)).ToList();
            var procedureIds = (await _documentRepository.GetProcedureIdsByDocumentIdAsync(id)).ToList();
            return MapToDocumentResponse(details, currentVersion, processIds, procedureIds);
        }

        public async Task<DocumentStatisticsResponse> GetStatisticsAsync(UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationScope = ResolveOrganizationScopeForRead(userContext, null);
            var statusFilter = ResolveReadableStatusFilter(null, userContext);

            int? restrictedUserId = (userContext.Role == UserRoles.UTILISATEUR)
                ? userContext.UserId
                : null;

            var documents = (await _documentRepository.SearchAsync(
                1,
                5000,
                null,
                null,
                statusFilter,
                null,
                null,
                null,
                organizationScope,
                pendingValidationOnly: false,
                hidePendingValidationFromGlobal: false,
                restrictedUserId)).ToList();

            return new DocumentStatisticsResponse
            {
                Total = documents.Count,
                Approved = documents.Where(d =>
                    d.Status == DocumentConstants.StatusApprouve ||
                    d.Status == DocumentConstants.StatusPublie).Count(),
                InReview = documents.Where(d => d.Status == DocumentConstants.StatusEnRevision).Count(),
                Expired = documents.Where(d => d.Status == DocumentConstants.StatusPerime).Count(),
                Draft = documents.Where(d => d.Status == DocumentConstants.StatusBrouillon).Count(),
                Archived = documents.Where(d => d.Status == DocumentConstants.StatusArchive).Count(),
                RecentlyUpdated = documents.Where(d => (d.UpdatedAt ?? d.CreatedAt) >= DateTime.UtcNow.AddDays(-30)).Count(),
                ByType = documents
                    .GroupBy(d => d.Type)
                    .ToDictionary(group => group.Key, group => group.Count()),
                ByProcess = documents
                    .GroupBy(d => string.IsNullOrWhiteSpace(d.ProcessCode) ? "SANS_PROCESSUS" : d.ProcessCode!)
                    .ToDictionary(group => group.Key, group => group.Count())
            };
        }

        public async Task<List<DocumentExpiringResponse>> GetExpiringAsync(int withinDays, UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationId = ResolveOrganizationScopeForRead(userContext, null)
                ?? throw new ForbiddenException("Organisation introuvable.");

            var effectiveWindow = withinDays <= 0 ? 30 : Math.Min(withinDays, 365);
            var data = await _documentRepository.GetExpiringAsync(organizationId, effectiveWindow);

            return data.Select(item => new DocumentExpiringResponse
            {
                Id = item.Id,
                OrganizationId = item.OrganizationId,
                Code = item.Code,
                Title = item.Title,
                Status = item.Status,
                VersionNumber = item.VersionNumber,
                ExpiryDate = item.ExpiryDate,
                DaysToExpiry = item.DaysToExpiry,
                ExpirationState = ResolveExpirationState(item.DaysToExpiry),
                OwnerUserId = item.OwnerUserId,
                OwnerFullName = item.OwnerFullName
            }).ToList();
        }

        public async Task<List<DocumentActionLogResponse>> GetActionLogsAsync(int documentId, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var document = await GetDocumentOrThrowAsync(documentId);
            EnsureDocumentAccess(userContext, document.OrganizationId);

            var logs = await _documentActionLogRepository.GetByDocumentIdAsync(documentId, document.OrganizationId);
            return logs.Select(MapToActionLogResponse).ToList();
        }

        public async Task<List<DocumentVersionResponse>> GetVersionsAsync(int documentId, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var document = await GetDocumentOrThrowAsync(documentId);
            EnsureDocumentAccess(userContext, document.OrganizationId);

            var versions = await _documentVersionRepository.GetByDocumentIdAsync(documentId);
            return versions.Select(MapToVersionResponse).ToList();
        }

        public async Task<DocumentVersionResponse> GetVersionByIdAsync(int documentId, int versionId, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var document = await GetDocumentOrThrowAsync(documentId);
            EnsureDocumentAccess(userContext, document.OrganizationId);

            var version = await _documentVersionRepository.GetByDocumentAndVersionIdAsync(documentId, versionId);
            if (version == null)
            {
                throw new NotFoundException("Version de document introuvable.");
            }

            var details = await _documentVersionRepository.GetDetailsByIdAsync(version.Id);
            if (details == null)
            {
                throw new NotFoundException("Version de document introuvable.");
            }

            return MapToVersionResponse(details);
        }

        public Task<DocumentVersionResponse> CreateVersionAsync(int documentId, CreateDocumentVersionRequest request, UserContext userContext)
        {
            throw new ServiceException("La creation de version sans fichier n'est pas autorisee. Veuillez televerser un document.");
        }

        private async Task<DatabaseDocumentFile> ReadDocumentFileForDatabaseAsync(
            IFormFile file)
        {
            if (file == null || file.Length <= 0)
            {
                throw new ServiceException("Le fichier a televerser est obligatoire.");
            }

            if (file.Length > _maxDocumentFileSizeBytes)
            {
                throw new ServiceException($"La taille du fichier depasse la limite autorisee ({_maxDocumentFileSizeBytes / 1024 / 1024} MB).");
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                throw new ServiceException("Le fichier doit avoir une extension valide.");
            }

            var normalizedExtension = extension.Trim().ToLowerInvariant();
            if (!SupportedDocumentFileExtensions.Contains(normalizedExtension))
            {
                throw new ServiceException("Seuls les fichiers PDF, Word (.docx) et Excel (.xlsx) sont autorises.");
            }

            if (_allowedDocumentExtensions.Count > 0 && !_allowedDocumentExtensions.Contains(normalizedExtension))
            {
                throw new ServiceException("Le type de fichier n'est pas autorise.");
            }

            await using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);

            var mimeType = file.ContentType;
            if (string.IsNullOrWhiteSpace(mimeType) || mimeType == "application/octet-stream")
            {
                mimeType = normalizedExtension.ToLowerInvariant() switch
                {
                    ".pdf" => "application/pdf",
                    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    _ => "application/octet-stream"
                };
            }

            return new DatabaseDocumentFile
            {
                FileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{normalizedExtension}",
                OriginalFileName = file.FileName,
                FileExtension = normalizedExtension,
                MimeType = mimeType,
                FileSize = memoryStream.Length,
                FileContent = memoryStream.ToArray()
            };
        }

        private async Task<byte[]> StampUploadedDocxAsync(byte[] fileContent, PdfHeaderMetadata headerMetadata)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");

            try
            {
                await File.WriteAllBytesAsync(tempPath, fileContent);
                await _wordHeaderStampService.ApplyFirstPageHeaderAsync(tempPath, headerMetadata);
                return await File.ReadAllBytesAsync(tempPath);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // Temporary file cleanup should not hide the upload result.
                }
            }
        }

        private async Task<byte[]> StampUploadedPdfAsync(byte[] fileContent, PdfHeaderMetadata headerMetadata)
        {
            using var sourceStream = new MemoryStream(fileContent, writable: false);
            var stampedStream = await _pdfHeaderStampService.AddHeaderAsync(sourceStream, headerMetadata);
            using var result = new MemoryStream();
            await stampedStream.CopyToAsync(result);
            return result.ToArray();
        }

        private async Task<byte[]> StampUploadedXlsxAsync(byte[] fileContent, PdfHeaderMetadata headerMetadata)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

            try
            {
                await File.WriteAllBytesAsync(tempPath, fileContent);
                await _excelHeaderStampService.ApplyWorkbookHeaderAsync(tempPath, headerMetadata);
                return await File.ReadAllBytesAsync(tempPath);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // Temporary file cleanup should not hide the upload result.
                }
            }
        }

        public async Task<DocumentVersionResponse> UploadVersionAsync(int documentId, UploadDocumentVersionRequest request, UserContext userContext)
        {
            EnsureCanSubmit(userContext);

            var document = await GetDocumentOrThrowAsync(documentId);
            EnsureDocumentSubmitAccess(userContext, document.OrganizationId);

            var isPrivilegedCreator = userContext.Role == UserRoles.ADMIN_ORG
                                   || userContext.Role == UserRoles.RESPONSABLE_QUALITE
                                   || userContext.IsSuperAdmin;

            if (!isPrivilegedCreator)
            {
                var userProcesses = await _processRepository.GetByOrganizationAsync(document.OrganizationId, userContext.UserId);
                if (userProcesses == null || !userProcesses.Any())
                {
                    throw new ForbiddenException("Vous devez être affecté à au moins un processus pour déposer une nouvelle version de document.");
                }
            }

            if (request.File == null)
            {
                throw new ServiceException("Le fichier est obligatoire pour televerser une version.");
            }

            var normalizedStatus = ResolveSubmittedStatus(request.Status, userContext);
            var versionNumber = await ResolveVersionNumberAsync(documentId, request.VersionNumber);
            await ValidateVersionPayloadAsync(documentId, versionNumber, normalizedStatus);

            var isPublished = normalizedStatus == DocumentConstants.StatusPublie;
            var isApproved = normalizedStatus == DocumentConstants.StatusApprouve || isPublished;

            var stored = await ReadDocumentFileForDatabaseAsync(
                request.File);

            // Bypassed stamping on upload to store the original, pristine file in the database
            /*
            if (string.Equals(stored.FileExtension, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var headerMetadata = await BuildPdfHeaderMetadataAsync(
                    document,
                    new DocumentVersionData
                    {
                        VersionNumber = versionNumber,
                        Status = normalizedStatus,
                        Signature = request.Signature,
                        EstablishedByUserId = userContext.UserId,
                        ValidatedByUserId = isApproved ? userContext.UserId : null
                    });
                stored.FileContent = await StampUploadedPdfAsync(stored.FileContent, headerMetadata);
                stored.FileSize = stored.FileContent.LongLength;
            }
            else if (string.Equals(stored.FileExtension, ".docx", StringComparison.OrdinalIgnoreCase))
            {
                var headerMetadata = await BuildPdfHeaderMetadataAsync(
                    document,
                    new DocumentVersionData
                    {
                        VersionNumber = versionNumber,
                        Status = normalizedStatus,
                        Signature = request.Signature,
                        EstablishedByUserId = userContext.UserId,
                        ValidatedByUserId = isApproved ? userContext.UserId : null
                    });
                stored.FileContent = await StampUploadedDocxAsync(stored.FileContent, headerMetadata);
                stored.FileSize = stored.FileContent.LongLength;
            }
            else if (string.Equals(stored.FileExtension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                var headerMetadata = await BuildPdfHeaderMetadataAsync(
                    document,
                    new DocumentVersionData
                    {
                        VersionNumber = versionNumber,
                        Status = normalizedStatus,
                        Signature = request.Signature,
                        EstablishedByUserId = userContext.UserId,
                        ValidatedByUserId = isApproved ? userContext.UserId : null
                    });
                stored.FileContent = await StampUploadedXlsxAsync(stored.FileContent, headerMetadata);
                stored.FileSize = stored.FileContent.LongLength;
            }
            */

            var now = DateTime.UtcNow;

            var version = new DocumentVersion
            {
                DocumentId = documentId,
                OrganizationId = document.OrganizationId,
                VersionNumber = versionNumber,
                Status = normalizedStatus,
                FileName = stored.FileName,
                OriginalFileName = stored.OriginalFileName,
                FilePath = null,
                FileExtension = stored.FileExtension,
                MimeType = stored.MimeType,
                FileSize = stored.FileSize,
                FileContent = stored.FileContent,
                RevisionComment = NormalizeNullable(request.RevisionComment),
                Signature = request.Signature,
                EstablishedByUserId = userContext.UserId,
                EstablishedAt = now,
                VerifiedByUserId = isApproved ? userContext.UserId : null,
                VerifiedAt = isApproved ? now : null,
                ValidatedByUserId = isApproved ? userContext.UserId : null,
                ValidatedAt = isApproved ? now : null,
                EffectiveDate = request.EffectiveDate,
                ExpiryDate = request.ExpiryDate,
                IsCurrent = isPublished,
                CreatedAt = now
            };

            var versionId = await _documentVersionRepository.CreateAsync(version);
            if (isPublished)
            {
                await _documentVersionRepository.SetCurrentVersionAsync(documentId, versionId);
                await _documentRepository.SetCurrentVersionAsync(documentId, versionId);
            }

            var details = await _documentVersionRepository.GetDetailsByIdAsync(versionId);
            if (details == null)
            {
                throw new ServiceException("Version televersee mais introuvable.");
            }
            await LogDocumentActionAsync(
                document,
                versionId,
                "VERSION_UPLOADED",
                null,
                details.VersionNumber,
                $"Version {details.VersionNumber} televersee with status {normalizedStatus}.",
                userContext.UserId);

            await _actionLogger.LogActionAsync(
                document.OrganizationId,
                userContext.UserId,
                $"{userContext.FirstName} {userContext.LastName}",
                "DOCUMENT",
                "VERSION_UPLOAD",
                $"Version {details.VersionNumber} : {document.Code}",
                $"Nouvelle version téléversée pour '{document.Title}'.");
            await PublishDocumentVersionNotificationsAsync(document, versionId, details.VersionNumber, normalizedStatus, userContext.UserId);

            return MapToVersionResponse(details);
        }

        public async Task<DocumentVersionResponse> UpdateVersionStatusAsync(int documentId, int versionId, UpdateDocumentVersionStatusRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            EnsureCanValidate(userContext);

            var document = await GetDocumentOrThrowAsync(documentId);
            EnsureDocumentWriteAccess(userContext, document.OrganizationId);

            var version = await _documentVersionRepository.GetByDocumentAndVersionIdAsync(documentId, versionId);
            if (version == null)
            {
                throw new NotFoundException("Version de document introuvable.");
            }

            var normalizedStatus = NormalizeUpper(request.Status);
            if (string.IsNullOrWhiteSpace(normalizedStatus) || !DocumentConstants.AllowedStatuses.Contains(normalizedStatus))
            {
                throw new ServiceException("Le statut de version est invalide.");
            }

            int? verifiedByUserId = null;
            DateTime? verifiedAt = null;
            int? validatedByUserId = null;
            DateTime? validatedAt = null;

            if (normalizedStatus == DocumentConstants.StatusEnRevision)
            {
                // Placing a document back to revision means it is awaiting new verification/validation.
                // We keep it as unverified (no verifiedByUserId / verifiedAt assigned).
            }

            if (normalizedStatus == DocumentConstants.StatusApprouve)
            {
                verifiedByUserId = userContext.UserId;
                verifiedAt = DateTime.UtcNow;
                validatedByUserId = userContext.UserId;
                validatedAt = DateTime.UtcNow;
            }

            if (normalizedStatus == DocumentConstants.StatusPublie)
            {
                if (!version.VerifiedByUserId.HasValue)
                {
                    verifiedByUserId = userContext.UserId;
                    verifiedAt = DateTime.UtcNow;
                }

                if (!version.ValidatedByUserId.HasValue)
                {
                    validatedByUserId = userContext.UserId;
                    validatedAt = DateTime.UtcNow;
                }

                if ((version.FileContent?.Length ?? 0) > 0 || !string.IsNullOrWhiteSpace(version.FilePath))
                {
                    await _documentVersionRepository.SetCurrentVersionAsync(documentId, versionId);
                    await _documentRepository.SetCurrentVersionAsync(documentId, versionId);
                }
            }

            await _documentVersionRepository.UpdateStatusAsync(
                versionId,
                normalizedStatus,
                NormalizeNullable(request.RevisionComment),
                verifiedByUserId,
                verifiedAt,
                validatedByUserId,
                validatedAt,
                DateTime.UtcNow);

            var updated = await _documentVersionRepository.GetDetailsByIdAsync(versionId);
            if (updated == null)
            {
                throw new NotFoundException("Version de document introuvable.");
            }
            await LogDocumentActionAsync(
                document,
                versionId,
                "VERSION_STATUS_CHANGED",
                version.Status,
                normalizedStatus,
                NormalizeNullable(request.RevisionComment),
                userContext.UserId);

            await _actionLogger.LogActionAsync(
                document.OrganizationId,
                userContext.UserId,
                $"{userContext.FirstName} {userContext.LastName}",
                "DOCUMENT",
                "STATUS_CHANGE",
                $"Statut v{updated.VersionNumber} : {document.Code} -> {normalizedStatus}",
                $"Le statut de la version {updated.VersionNumber} a été mis à jour.");
            await PublishDocumentVersionNotificationsAsync(document, versionId, updated.VersionNumber, normalizedStatus, userContext.UserId);

            return MapToVersionResponse(updated);
        }

        private async Task PublishDocumentVersionNotificationsAsync(Document document, int versionId, string versionNumber, string normalizedStatus, int triggeredByUserId)
        {
            var targetUserIds = new List<int>();
            if (document.OwnerUserId.HasValue && document.OwnerUserId.Value > 0)
            {
                targetUserIds.Add(document.OwnerUserId.Value);
            }

            if (string.Equals(normalizedStatus, DocumentConstants.StatusEnRevision, StringComparison.OrdinalIgnoreCase))
            {
                await _notificationEventPublisher.PublishToRolesAsync(
                    document.OrganizationId,
                    new[] { UserRoles.RESPONSABLE_QUALITE },
                    NotificationConstants.TypeDocumentApprovalRequired,
                    NotificationConstants.CategoryInfo,
                    $"Validation requise pour {document.Code}",
                    $"Le document {document.Title} ({versionNumber}) est en revision et attend validation.",
                    NotificationConstants.PriorityMedium,
                    "DOCUMENT",
                    document.Id.ToString(),
                    $"/documents/{document.Id}/versions",
                    triggeredByUserId);
            }

            if (string.Equals(normalizedStatus, DocumentConstants.StatusPerime, StringComparison.OrdinalIgnoreCase))
            {
                await _notificationEventPublisher.PublishToRolesAsync(
                    document.OrganizationId,
                    new[] { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE },
                    NotificationConstants.TypeDocumentExpired,
                    NotificationConstants.CategoryWarning,
                    $"Document expire {document.Code}",
                    $"Le document {document.Title} ({versionNumber}) est expire/perime.",
                    NotificationConstants.PriorityHigh,
                    "DOCUMENT",
                    document.Id.ToString(),
                    $"/documents/{document.Id}",
                    triggeredByUserId);

                await _notificationEventPublisher.PublishToUsersAsync(
                    document.OrganizationId,
                    targetUserIds,
                    NotificationConstants.TypeDocumentExpired,
                    NotificationConstants.CategoryWarning,
                    $"Document expire {document.Code}",
                    $"Votre document {document.Title} ({versionNumber}) est expire/perime.",
                    NotificationConstants.PriorityHigh,
                    "DOCUMENT",
                    document.Id.ToString(),
                    $"/documents/{document.Id}",
                    triggeredByUserId);
            }

            if (string.Equals(normalizedStatus, DocumentConstants.StatusApprouve, StringComparison.OrdinalIgnoreCase))
            {
                await _notificationEventPublisher.PublishToRolesAsync(
                    document.OrganizationId,
                    new[] { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE },
                    NotificationConstants.TypeSystemAlert,
                    NotificationConstants.CategorySuccess,
                    $"Document approuve {document.Code}",
                    $"Le document {document.Title} ({versionNumber}) a ete approuve et attend publication.",
                    NotificationConstants.PriorityMedium,
                    "DOCUMENT",
                    document.Id.ToString(),
                    $"/documents/{document.Id}/versions",
                    triggeredByUserId);

                await _notificationEventPublisher.PublishToUsersAsync(
                    document.OrganizationId,
                    targetUserIds,
                    NotificationConstants.TypeSystemAlert,
                    NotificationConstants.CategorySuccess,
                    $"Document approuve {document.Code}",
                    $"Votre document {document.Title} ({versionNumber}) a ete approuve et attend publication.",
                    NotificationConstants.PriorityMedium,
                    "DOCUMENT",
                    document.Id.ToString(),
                    $"/documents/{document.Id}/versions",
                    triggeredByUserId);
            }

            if (string.Equals(normalizedStatus, DocumentConstants.StatusPublie, StringComparison.OrdinalIgnoreCase))
            {
                await _notificationEventPublisher.PublishToRolesAsync(
                    document.OrganizationId,
                    new[] { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE, UserRoles.UTILISATEUR },
                    NotificationConstants.TypeDocumentNewVersion,
                    NotificationConstants.CategorySuccess,
                    $"Nouvelle version publiee {document.Code}",
                    $"Le document {document.Title} ({versionNumber}) a ete approuve et publie.",
                    NotificationConstants.PriorityMedium,
                    "DOCUMENT",
                    document.Id.ToString(),
                    $"/documents/{document.Id}/versions",
                    triggeredByUserId);

                await _notificationEventPublisher.PublishToUsersAsync(
                    document.OrganizationId,
                    targetUserIds,
                    NotificationConstants.TypeDocumentNewVersion,
                    NotificationConstants.CategorySuccess,
                    $"Nouvelle version publiee {document.Code}",
                    $"Votre document {document.Title} ({versionNumber}) a ete approuve et publie.",
                    NotificationConstants.PriorityMedium,
                    "DOCUMENT",
                    document.Id.ToString(),
                    $"/documents/{document.Id}/versions",
                    triggeredByUserId);
            }

            if (string.Equals(normalizedStatus, DocumentConstants.StatusRejete, StringComparison.OrdinalIgnoreCase))
            {
                await _notificationEventPublisher.PublishToRolesAsync(
                    document.OrganizationId,
                    new[] { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE },
                    NotificationConstants.TypeSystemAlert,
                    NotificationConstants.CategoryWarning,
                    $"Document rejete {document.Code}",
                    $"Le document {document.Title} ({versionNumber}) a ete rejete.",
                    NotificationConstants.PriorityHigh,
                    "DOCUMENT",
                    document.Id.ToString(),
                    $"/documents/{document.Id}/versions",
                    triggeredByUserId);
            }

            if (string.Equals(normalizedStatus, DocumentConstants.StatusArchive, StringComparison.OrdinalIgnoreCase))
            {
                await _notificationEventPublisher.PublishToRolesAsync(
                    document.OrganizationId,
                    new[] { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE, UserRoles.UTILISATEUR },
                    NotificationConstants.TypeSystemAlert,
                    NotificationConstants.CategoryInfo,
                    $"Document archive {document.Code}",
                    $"Le document {document.Title} ({versionNumber}) a ete archive.",
                    NotificationConstants.PriorityMedium,
                    "DOCUMENT",
                    document.Id.ToString(),
                    $"/documents/{document.Id}",
                    triggeredByUserId);
            }

        }

        public async Task<(Stream Stream, string ContentType, string FileName)> DownloadCurrentAsync(int documentId, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var document = await GetDocumentOrThrowAsync(documentId);
            await EnsureDocumentReadAccessAsync(document, userContext);

            var current = await _documentVersionRepository.GetCurrentByDocumentIdAsync(documentId);
            if (current == null)
            {
                throw new NotFoundException("Aucune version courante disponible pour ce document.");
            }

            var stream = await OpenVersionContentAsync(current.Id, current.FilePath, "Aucun fichier n'est attache a la version courante.");
            var outputStream = stream;
            var contentType = string.IsNullOrWhiteSpace(current.MimeType) ? "application/octet-stream" : current.MimeType;
            var fileName = string.IsNullOrWhiteSpace(current.OriginalFileName)
                ? $"{document.Code}_{current.VersionNumber}"
                : current.OriginalFileName;

            if (ShouldStampPdf(current.MimeType, current.OriginalFileName))
            {
                outputStream = await StampPdfWithHeaderAsync(stream, document, current);
                stream.Dispose();
                contentType = "application/pdf";
                fileName = EnsurePdfExtension(fileName, document.Code, current.VersionNumber);
            }
            else if (ShouldStampDocx(current.MimeType, current.OriginalFileName))
            {
                outputStream = await StampDocxWithHeaderAsync(stream, document, current);
                stream.Dispose();
                contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                fileName = EnsureDocxExtension(fileName, document.Code, current.VersionNumber);
            }
            else if (ShouldStampXlsx(current.MimeType, current.OriginalFileName))
            {
                outputStream = await StampXlsxWithHeaderAsync(stream, document, current);
                stream.Dispose();
                contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                fileName = EnsureXlsxExtension(fileName, document.Code, current.VersionNumber);
            }
            else if (ShouldConvertTextToPdf(current.MimeType, current.OriginalFileName))
            {
                outputStream = await ConvertTextToPdfWithHeaderAsync(stream, document, current);
                stream.Dispose();
                contentType = "application/pdf";
                fileName = EnsurePdfExtension(fileName, document.Code, current.VersionNumber);
            }

            await LogDocumentActionAsync(
                document,
                current.Id,
                "CURRENT_VERSION_DOWNLOADED",
                null,
                current.VersionNumber,
                $"Telechargement de la version courante {current.VersionNumber}.",
                userContext.UserId);

            return (
                outputStream,
                contentType,
                fileName
            );
        }

        public async Task<(Stream Stream, string ContentType, string FileName)> DownloadVersionAsync(int documentId, int versionId, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var document = await GetDocumentOrThrowAsync(documentId);
            await EnsureDocumentReadAccessAsync(document, userContext);

            var version = await _documentVersionRepository.GetDetailsByIdAsync(versionId);
            if (version == null || version.DocumentId != documentId)
            {
                throw new NotFoundException("Version de document introuvable.");
            }

            var stream = await OpenVersionContentAsync(version.Id, version.FilePath, "Aucun fichier n'est attache a cette version.");
            var outputStream = stream;
            var contentType = string.IsNullOrWhiteSpace(version.MimeType) ? "application/octet-stream" : version.MimeType;
            var fileName = string.IsNullOrWhiteSpace(version.OriginalFileName)
                ? $"{document.Code}_{version.VersionNumber}"
                : version.OriginalFileName;

            if (ShouldStampPdf(version.MimeType, version.OriginalFileName))
            {
                outputStream = await StampPdfWithHeaderAsync(stream, document, version);
                stream.Dispose();
                contentType = "application/pdf";
                fileName = EnsurePdfExtension(fileName, document.Code, version.VersionNumber);
            }
            else if (ShouldStampDocx(version.MimeType, version.OriginalFileName))
            {
                outputStream = await StampDocxWithHeaderAsync(stream, document, version);
                stream.Dispose();
                contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                fileName = EnsureDocxExtension(fileName, document.Code, version.VersionNumber);
            }
            else if (ShouldStampXlsx(version.MimeType, version.OriginalFileName))
            {
                outputStream = await StampXlsxWithHeaderAsync(stream, document, version);
                stream.Dispose();
                contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                fileName = EnsureXlsxExtension(fileName, document.Code, version.VersionNumber);
            }
            else if (ShouldConvertTextToPdf(version.MimeType, version.OriginalFileName))
            {
                outputStream = await ConvertTextToPdfWithHeaderAsync(stream, document, version);
                stream.Dispose();
                contentType = "application/pdf";
                fileName = EnsurePdfExtension(fileName, document.Code, version.VersionNumber);
            }

            await LogDocumentActionAsync(
                document,
                version.Id,
                "VERSION_DOWNLOADED",
                null,
                version.VersionNumber,
                $"Telechargement de la version {version.VersionNumber}.",
                userContext.UserId);

            return (
                outputStream,
                contentType,
                fileName
            );
        }

        public async Task<(Stream Stream, string ContentType)> PreviewCurrentAsync(int documentId, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var document = await GetDocumentOrThrowAsync(documentId);
            await EnsureDocumentReadAccessAsync(document, userContext);

            var current = await _documentVersionRepository.GetCurrentByDocumentIdAsync(documentId);
            if (current == null)
            {
                throw new NotFoundException("Aucune version courante disponible pour ce document.");
            }

            var stream = await OpenVersionContentAsync(current.Id, current.FilePath, "Aucun fichier n'est attache a la version courante.");
            var outputStream = stream;
            var contentType = string.IsNullOrWhiteSpace(current.MimeType) ? "application/octet-stream" : current.MimeType;
            if (ShouldStampPdf(current.MimeType, current.OriginalFileName))
            {
                outputStream = await StampPdfWithHeaderAsync(stream, document, current);
                stream.Dispose();
                contentType = "application/pdf";
            }
            else if (ShouldStampDocx(current.MimeType, current.OriginalFileName))
            {
                outputStream = await StampDocxWithHeaderAsync(stream, document, current);
                stream.Dispose();
                contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            }
            else if (ShouldStampXlsx(current.MimeType, current.OriginalFileName))
            {
                outputStream = await StampXlsxWithHeaderAsync(stream, document, current);
                stream.Dispose();
                contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            }
            else if (ShouldConvertTextToPdf(current.MimeType, current.OriginalFileName))
            {
                outputStream = await ConvertTextToPdfWithHeaderAsync(stream, document, current);
                stream.Dispose();
                contentType = "application/pdf";
            }

            await LogDocumentActionAsync(
                document,
                current.Id,
                "CURRENT_VERSION_PREVIEWED",
                null,
                current.VersionNumber,
                $"Apercu de la version courante {current.VersionNumber}.",
                userContext.UserId);

            return (
                outputStream,
                contentType
            );
        }

        private async Task<Stream> StampPdfWithHeaderAsync(Stream sourceStream, Document document, DocumentVersionData version)
        {
            var metadata = await BuildPdfHeaderMetadataAsync(document, version);
            return await _pdfHeaderStampService.AddHeaderAsync(sourceStream, metadata);
        }

        private async Task<Stream> ConvertTextToPdfWithHeaderAsync(Stream sourceStream, Document document, DocumentVersionData version)
        {
            using var reader = new StreamReader(sourceStream, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true);
            var textContent = await reader.ReadToEndAsync();
            if (sourceStream.CanSeek)
            {
                sourceStream.Position = 0;
            }

            var metadata = await BuildPdfHeaderMetadataAsync(document, version);
            return await _pdfHeaderStampService.CreatePdfFromTextAsync(textContent, metadata);
        }

        private async Task<Stream> StampDocxWithHeaderAsync(Stream sourceStream, Document document, DocumentVersionData version)
        {
            var metadata = await BuildPdfHeaderMetadataAsync(document, version);
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");

            try
            {
                if (sourceStream.CanSeek)
                {
                    sourceStream.Position = 0;
                }

                await using (var tempWrite = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await sourceStream.CopyToAsync(tempWrite);
                }

                await _wordHeaderStampService.ApplyFirstPageHeaderAsync(tempPath, metadata);

                var bytes = await File.ReadAllBytesAsync(tempPath);
                return new MemoryStream(bytes);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // Keep response flow resilient if temp cleanup fails.
                }
            }
        }

        private async Task<Stream> StampXlsxWithHeaderAsync(Stream sourceStream, Document document, DocumentVersionData version)
        {
            var metadata = await BuildPdfHeaderMetadataAsync(document, version);
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

            try
            {
                if (sourceStream.CanSeek)
                {
                    sourceStream.Position = 0;
                }

                await using (var tempWrite = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await sourceStream.CopyToAsync(tempWrite);
                }

                await _excelHeaderStampService.ApplyWorkbookHeaderAsync(tempPath, metadata);

                var bytes = await File.ReadAllBytesAsync(tempPath);
                return new MemoryStream(bytes);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // Keep response flow resilient if temp cleanup fails.
                }
            }
        }

        private async Task<PdfHeaderMetadata> BuildPdfHeaderMetadataAsync(Document document, DocumentVersionData version)
        {
            var organization = await _organizationRepository.GetByIdAsync(document.OrganizationId);
            var process = document.ProcessId.HasValue
                ? await _processRepository.GetByIdAsync(document.ProcessId.Value)
                : null;
            var procedure = document.ProcedureId.HasValue
                ? await _procedureRepository.GetByIdAsync(document.ProcedureId.Value)
                : null;
            
            var signerRole = string.Empty;
            if (version.ValidatedByUserId.HasValue)
            {
                var signer = await _userRepository.GetByIdAsync(version.ValidatedByUserId.Value);
                if (signer != null)
                {
                    signerRole = signer.Role switch
                    {
                        UserRoles.SUPER_ADMIN => "Super Admin",
                        UserRoles.ADMIN_ORG => "Admin",
                        UserRoles.RESPONSABLE_QUALITE => "Responsable Qualité",
                        UserRoles.UTILISATEUR => "Utilisateur",
                        _ => signer.Role ?? string.Empty
                    };
                }
            }

            if (string.IsNullOrEmpty(signerRole))
            {
                var userId = version.ValidatedByUserId ?? version.EstablishedByUserId;
                if (userId == 0 && document.OwnerUserId.HasValue)
                {
                    userId = document.OwnerUserId.Value;
                }

                if (userId > 0)
                {
                    var signer = await _userRepository.GetByIdAsync(userId);
                    if (signer != null)
                    {
                        signerRole = signer.Role switch
                        {
                            UserRoles.SUPER_ADMIN => "Super Admin",
                            UserRoles.ADMIN_ORG => "Admin",
                            UserRoles.RESPONSABLE_QUALITE => "Responsable Qualité",
                            UserRoles.UTILISATEUR => "Utilisateur",
                            _ => signer.Role ?? string.Empty
                        };
                    }
                }
            }

            if (string.IsNullOrEmpty(signerRole))
            {
                signerRole = "Collaborateur";
            }

            return new PdfHeaderMetadata
            {
                OrganizationName = organization?.Name ?? "Organisation",
                OrganizationCode = organization?.Code ?? string.Empty,
                OrganizationLogoPath = organization?.LogoPath,
                ProcessCode = process?.Code ?? "-",
                ProcedureCode = procedure?.Code ?? "-",
                DocumentCode = document.Code,
                DocumentTitle = document.Title,
                VersionNumber = version.VersionNumber,
                Status = version.Status,
                SignatureBase64 = version.Signature ?? document.Signature,
                SignerRole = signerRole,
                GeneratedAtUtc = DateTime.UtcNow
            };
        }

        private async Task ValidateDocumentPayloadAsync(
            int? processId,
            int? procedureId,
            string code,
            string title,
            string type,
            int? ownerUserId,
            int organizationId,
            int? excludeDocumentId)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ServiceException("Le code document est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ServiceException("Le titre du document est obligatoire.");
            }

            var normalizedType = NormalizeUpper(type);
            if (string.IsNullOrWhiteSpace(normalizedType) || !DocumentConstants.AllowedTypes.Contains(normalizedType))
            {
                throw new ServiceException("Le type de document est invalide.");
            }

            var codeExists = await _documentRepository.ExistsCodeAsync(organizationId, code.Trim(), excludeDocumentId);
            if (codeExists)
            {
                throw new ServiceException("Ce code document existe deja dans l'organisation.");
            }

            if (processId.HasValue)
            {
                var process = await _processRepository.GetByIdAsync(processId.Value);
                if (process == null)
                {
                    throw new ServiceException("Le processus selectionne est introuvable.");
                }

                if (process.OrganizationId != organizationId)
                {
                    throw new ForbiddenException("Le processus doit appartenir a la meme organisation.");
                }
            }

            if (procedureId.HasValue)
            {
                var procedure = await _procedureRepository.GetByIdAsync(procedureId.Value);
                if (procedure == null)
                {
                    throw new ServiceException("La procedure selectionnee est introuvable.");
                }

                if (procedure.OrganizationId != organizationId)
                {
                    throw new ForbiddenException("La procedure doit appartenir a la meme organisation.");
                }

                if (processId.HasValue && procedure.ProcessId != processId.Value)
                {
                    throw new ServiceException("La procedure n'appartient pas au processus selectionne.");
                }
            }

            if (ownerUserId.HasValue)
            {
                var owner = await _userRepository.GetByIdAsync(ownerUserId.Value);
                if (owner == null || !owner.IsActive)
                {
                    throw new ServiceException("Le responsable selectionne est invalide ou inactif.");
                }

                if (owner.OrganizationId != organizationId)
                {
                    throw new ForbiddenException("Le responsable doit appartenir a la meme organisation.");
                }
            }
        }

        private async Task<string> ResolveVersionNumberAsync(int documentId, string? requestedVersionNumber)
        {
            if (!string.IsNullOrWhiteSpace(requestedVersionNumber))
            {
                return requestedVersionNumber.Trim();
            }

            var versions = (await _documentVersionRepository.GetByDocumentIdAsync(documentId)).ToList();
            if (versions.Count == 0)
            {
                return "v1.0";
            }

            var bestMajor = 1;
            var bestMinor = -1;

            foreach (var version in versions)
            {
                if (!TryParseVersionNumber(version.VersionNumber, out var major, out var minor))
                {
                    continue;
                }

                if (major > bestMajor || (major == bestMajor && minor > bestMinor))
                {
                    bestMajor = major;
                    bestMinor = minor;
                }
            }

            if (bestMinor < 0)
            {
                bestMajor = 1;
                bestMinor = versions.Count - 1;
            }

            var nextMajor = bestMajor;
            var nextMinor = bestMinor + 1;
            string candidate;
            do
            {
                candidate = $"v{nextMajor}.{nextMinor}";
                nextMinor++;
            }
            while (await _documentVersionRepository.ExistsVersionNumberAsync(documentId, candidate));

            return candidate;
        }

        private static bool TryParseVersionNumber(string? value, out int major, out int minor)
        {
            major = 0;
            minor = 0;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[1..];
            }

            var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 2
                && int.TryParse(parts[0], out major)
                && int.TryParse(parts[1], out minor)
                && major >= 0
                && minor >= 0;
        }

        private async Task ValidateVersionPayloadAsync(int documentId, string versionNumber, string status)
        {
            if (string.IsNullOrWhiteSpace(versionNumber))
            {
                throw new ServiceException("Le numero de version est obligatoire.");
            }

            var normalizedStatus = NormalizeUpper(status);
            if (string.IsNullOrWhiteSpace(normalizedStatus) || !DocumentConstants.AllowedStatuses.Contains(normalizedStatus))
            {
                throw new ServiceException("Le statut de version est invalide.");
            }

            var exists = await _documentVersionRepository.ExistsVersionNumberAsync(documentId, versionNumber.Trim());
            if (exists)
            {
                throw new ServiceException("Ce numero de version existe deja pour ce document.");
            }
        }

        private async Task<Document> GetDocumentOrThrowAsync(int id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                throw new NotFoundException("Document introuvable.");
            }

            return document;
        }
        private static string ResolveExpirationState(int? daysToExpiry)
        {
            if (!daysToExpiry.HasValue)
            {
                return "VALID";
            }

            if (daysToExpiry.Value < 0)
            {
                return "EXPIRED";
            }

            if (daysToExpiry.Value <= 30)
            {
                return "EXPIRING_SOON";
            }

            return "VALID";
        }

        private static DocumentListItemResponse MapToListItemResponse(DocumentListItemData item)
        {
            var daysToExpiry = item.ExpiryDate.HasValue
                ? (int?)(item.ExpiryDate.Value.Date - DateTime.UtcNow.Date).TotalDays
                : null;
            var daysUntilPermanentDelete = item.DeletedAt.HasValue
                ? Math.Max(0, TrashRetentionDays - (int)Math.Floor((DateTime.UtcNow - item.DeletedAt.Value).TotalDays))
                : (int?)null;

            return new DocumentListItemResponse
            {
                Id = item.Id,
                OrganizationId = item.OrganizationId,
                Code = item.Code,
                Title = item.Title,
                Type = item.Type,
                ProcessId = item.ProcessId,
                ProcessCode = item.ProcessCode,
                ProcessName = item.ProcessName,
                ProcedureId = item.ProcedureId,
                ProcedureCode = item.ProcedureCode,
                ProcessIds = item.ProcessId.HasValue ? new List<int> { item.ProcessId.Value } : new List<int>(),
                ProcedureIds = item.ProcedureId.HasValue ? new List<int> { item.ProcedureId.Value } : new List<int>(),
                Status = string.IsNullOrWhiteSpace(item.Status) ? DocumentConstants.StatusBrouillon : item.Status,
                VersionNumber = item.VersionNumber,
                ExpiryDate = item.ExpiryDate,
                DaysToExpiry = daysToExpiry,
                ExpirationState = ResolveExpirationState(daysToExpiry),
                UpdatedAt = item.UpdatedAt ?? item.CreatedAt,
                OwnerUserId = item.OwnerUserId,
                OwnerFullName = item.OwnerFullName,
                FileName = item.FileName,
                IsActive = item.IsActive,
                DeletedAt = item.DeletedAt,
                DaysUntilPermanentDelete = daysUntilPermanentDelete
            };
        }

        private static DocumentResponse MapToDocumentResponse(DocumentDetailsData details, DocumentVersionResponse? currentVersion, List<int>? processIds = null, List<int>? procedureIds = null)
        {
            return new DocumentResponse
            {
                Id = details.Id,
                OrganizationId = details.OrganizationId,
                ProcessId = details.ProcessId,
                ProcessCode = details.ProcessCode,
                ProcessName = details.ProcessName,
                ProcedureId = details.ProcedureId,
                ProcedureCode = details.ProcedureCode,
                ProcedureTitle = details.ProcedureTitle,
                ProcessIds = processIds ?? new List<int>(),
                ProcedureIds = procedureIds ?? new List<int>(),
                Code = details.Code,
                Title = details.Title,
                Type = details.Type,
                Description = details.Description,
                Category = details.Category,
                Keywords = details.Keywords,
                OwnerUserId = details.OwnerUserId,
                OwnerFullName = details.OwnerFullName,
                CurrentVersionId = details.CurrentVersionId,
                CurrentVersionNumber = currentVersion?.VersionNumber,
                CurrentVersionStatus = currentVersion?.Status,
                IsActive = details.IsActive,
                CreatedAt = details.CreatedAt,
                UpdatedAt = details.UpdatedAt
            };
        }

        private static DocumentActionLogResponse MapToActionLogResponse(DocumentActionLogData actionLog)
        {
            return new DocumentActionLogResponse
            {
                Id = actionLog.Id,
                OrganizationId = actionLog.OrganizationId,
                DocumentId = actionLog.DocumentId,
                DocumentVersionId = actionLog.DocumentVersionId,
                VersionNumber = actionLog.VersionNumber,
                ActionType = actionLog.ActionType,
                OldValue = actionLog.OldValue,
                NewValue = actionLog.NewValue,
                Comment = actionLog.Comment,
                PerformedByUserId = actionLog.PerformedByUserId,
                PerformedByFullName = actionLog.PerformedByFullName,
                PerformedAt = actionLog.PerformedAt
            };
        }

        private static DocumentVersionResponse MapToVersionResponse(DocumentVersionData version)
        {
            return new DocumentVersionResponse
            {
                Id = version.Id,
                DocumentId = version.DocumentId,
                OrganizationId = version.OrganizationId,
                VersionNumber = version.VersionNumber,
                Status = version.Status,
                FileName = version.FileName,
                OriginalFileName = version.OriginalFileName,
                MimeType = version.MimeType,
                FileSize = version.FileSize,
                RevisionComment = version.RevisionComment,
                Signature = version.Signature,
                EffectiveDate = version.EffectiveDate,
                ExpiryDate = version.ExpiryDate,
                IsCurrent = version.IsCurrent,
                EstablishedByUserId = version.EstablishedByUserId,
                EstablishedByUser = version.EstablishedByUserFullName,
                EstablishedAt = version.EstablishedAt,
                VerifiedByUserId = version.VerifiedByUserId,
                VerifiedByUser = version.VerifiedByUserFullName,
                VerifiedAt = version.VerifiedAt,
                ValidatedByUserId = version.ValidatedByUserId,
                ValidatedByUser = version.ValidatedByUserFullName,
                ValidatedAt = version.ValidatedAt,
                CreatedAt = version.CreatedAt,
                UpdatedAt = version.UpdatedAt
            };
        }

        private async Task LogDocumentActionAsync(
            Document document,
            int? documentVersionId,
            string actionType,
            string? oldValue,
            string? newValue,
            string? comment,
            int performedByUserId)
        {
            await _documentActionLogRepository.CreateAsync(new DocumentActionLog
            {
                OrganizationId = document.OrganizationId,
                DocumentId = document.Id,
                DocumentVersionId = documentVersionId,
                ActionType = actionType,
                OldValue = oldValue,
                NewValue = newValue,
                Comment = comment,
                PerformedByUserId = performedByUserId,
                PerformedAt = DateTime.UtcNow
            });
        }

        private async Task EnsureDocumentReadAccessAsync(Document document, UserContext userContext)
        {
            EnsureDocumentAccess(userContext, document.OrganizationId);

            if (userContext.Role == UserRoles.UTILISATEUR)
            {
                var processIds = (await _documentRepository.GetProcessIdsByDocumentIdAsync(document.Id)).ToList();
                if (document.ProcessId.HasValue)
                {
                    processIds.Add(document.ProcessId.Value);
                }
                processIds = processIds.Distinct().ToList();

                if (processIds.Any())
                {
                    bool hasAccess = false;
                    foreach (var pid in processIds)
                    {
                        var process = await _processRepository.GetByIdAsync(pid);
                        if (process != null)
                        {
                            var isPilot = process.PilotUserId == userContext.UserId;
                            var isActor = await _processActorRepository.HasActorAsync(process.Id, userContext.UserId);
                            if (isPilot || isActor)
                            {
                                hasAccess = true;
                                break;
                            }
                        }
                    }

                    if (!hasAccess)
                    {
                        throw new ForbiddenException("Vous n'avez pas acces a ce document car vous n'etes ni le pilote ni un acteur de son processus associe.");
                    }
                }
            }
        }

        private static void EnsureCanRead(UserContext userContext)
        {
            if (!userContext.CanReadDocuments)
            {
                throw new ForbiddenException("Vous n'avez pas les droits de lecture sur les documents.");
            }
        }

        private static void EnsureCanWrite(UserContext userContext)
        {
            if (!userContext.CanWriteDocuments)
            {
                throw new ForbiddenException("Vous n'avez pas les droits d'ecriture sur les documents.");
            }
        }

        private static void EnsureCanSubmit(UserContext userContext)
        {
            if (!userContext.CanSubmitDocuments)
            {
                throw new ForbiddenException("Vous n'avez pas les droits de depot sur les documents.");
            }
        }

        private static void EnsureCanValidate(UserContext userContext)
        {
            if (userContext.Role != UserRoles.ADMIN_ORG && userContext.Role != UserRoles.RESPONSABLE_QUALITE)
            {
                throw new ForbiddenException("Seul le responsable peut valider et publier les documents.");
            }
        }

        private static int ResolveOrganizationScopeForWrite(UserContext userContext)
        {
            if (!userContext.OrganizationId.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable dans le token utilisateur.");
            }

            return userContext.OrganizationId.Value;
        }

        private static int? ResolveOrganizationScopeForRead(UserContext userContext, int? requestedOrganizationId)
        {
            if (!userContext.OrganizationId.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable dans le token utilisateur.");
            }

            if (requestedOrganizationId.HasValue && requestedOrganizationId.Value != userContext.OrganizationId.Value)
            {
                throw new ForbiddenException("Acces refuse a l'organisation demandee.");
            }

            return userContext.OrganizationId.Value;
        }

        private static void EnsureDocumentAccess(UserContext userContext, int organizationId)
        {
            if (!userContext.OrganizationId.HasValue || userContext.OrganizationId.Value != organizationId)
            {
                throw new ForbiddenException("Acces refuse a ce document.");
            }
        }

        private static void EnsureDocumentWriteAccess(UserContext userContext, int organizationId)
        {
            EnsureCanWrite(userContext);
            EnsureDocumentAccess(userContext, organizationId);
        }

        private async Task VerifyDocumentWritePermissionAsync(int? processId, UserContext userContext)
        {
            if (userContext.IsSuperAdmin || userContext.Role == UserRoles.RESPONSABLE_QUALITE)
            {
                return;
            }

            if (userContext.Role == UserRoles.UTILISATEUR)
            {
                if (!processId.HasValue)
                {
                    return;
                }

                var process = await _processRepository.GetByIdAsync(processId.Value);
                if (process == null)
                {
                    throw new NotFoundException("Processus associe introuvable.");
                }

                if (process.PilotUserId == userContext.UserId)
                {
                    return;
                }

                var actors = await _processActorRepository.GetActorsByProcessIdAsync(processId.Value);
                var userActor = actors.FirstOrDefault(a => a.UserId == userContext.UserId);
                if (userActor != null)
                {
                    var type = userActor.ActorType.Trim().ToUpperInvariant();
                    if (type == ProcessConstants.ActorPilote || type == ProcessConstants.ActorCopilote || type == ProcessConstants.ActorContributeur)
                    {
                        return;
                    }
                }

                throw new ForbiddenException("Vous n'avez pas les droits de modification sur les documents de ce processus car les observateurs ne peuvent pas modifier.");
            }
        }

        private static void EnsureDocumentSubmitAccess(UserContext userContext, int organizationId)
        {
            EnsureCanSubmit(userContext);
            EnsureDocumentAccess(userContext, organizationId);
        }

        private static string? NormalizeSearch(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static string? NormalizeUpper(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim().ToUpperInvariant();
        }

        private static string ResolveSubmittedStatus(string? requestedStatus, UserContext userContext)
        {
            var normalizedStatus = NormalizeUpper(requestedStatus) ?? DocumentConstants.StatusEnRevision;
            var canValidateDirectly = userContext.Role == UserRoles.ADMIN_ORG
                || userContext.Role == UserRoles.RESPONSABLE_QUALITE;
            if (!canValidateDirectly)
            {
                return DocumentConstants.StatusEnRevision;
            }

            return normalizedStatus;
        }

        private static string? ResolveReadableStatusFilter(string? requestedStatus, UserContext userContext)
        {
            if (userContext.Role == UserRoles.SUPER_ADMIN || userContext.Role == UserRoles.ADMIN_ORG || userContext.Role == UserRoles.RESPONSABLE_QUALITE)
            {
                return NormalizeUpper(requestedStatus);
            }

            var normalizedRequested = NormalizeUpper(requestedStatus);
            if (!string.IsNullOrWhiteSpace(normalizedRequested))
            {
                return normalizedRequested;
            }

            return UserVisibleStatusesFilter;
        }

        private static string? NormalizeNullable(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static bool ShouldStampPdf(string? mimeType, string? fileName)
        {
            if (!string.IsNullOrWhiteSpace(mimeType) &&
                mimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(fileName) &&
                fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool ShouldConvertTextToPdf(string? mimeType, string? fileName)
        {
            if (!string.IsNullOrWhiteSpace(mimeType) &&
                mimeType.Contains("text/plain", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(fileName) &&
                fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool ShouldStampDocx(string? mimeType, string? fileName)
        {
            if (!string.IsNullOrWhiteSpace(mimeType) &&
                mimeType.Contains("wordprocessingml.document", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(fileName) &&
                fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool ShouldStampXlsx(string? mimeType, string? fileName)
        {
            if (!string.IsNullOrWhiteSpace(mimeType) &&
                mimeType.Contains("spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(fileName) &&
                fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private async Task<Stream> OpenVersionContentAsync(int versionId, string? legacyFilePath, string notFoundMessage)
        {
            var fileContent = await _documentVersionRepository.GetFileContentAsync(versionId);
            if (fileContent != null && fileContent.Length > 0)
            {
                return new MemoryStream(fileContent, writable: false);
            }

            if (!string.IsNullOrWhiteSpace(legacyFilePath))
            {
                return await _fileStorageService.OpenReadAsync(legacyFilePath);
            }

            if (fileContent == null || fileContent.Length == 0)
            {
                throw new NotFoundException(notFoundMessage);
            }

            throw new NotFoundException(notFoundMessage);
        }

        private static HashSet<string> ParseAllowedExtensions(string? configValue)
        {
            if (string.IsNullOrWhiteSpace(configValue))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return configValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ext => ext.StartsWith('.') ? ext.ToLowerInvariant() : $".{ext.ToLowerInvariant()}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string EnsurePdfExtension(string? originalFileName, string documentCode, string versionNumber)
        {
            var baseName = string.IsNullOrWhiteSpace(originalFileName)
                ? $"{documentCode}_{versionNumber}"
                : originalFileName.Trim();

            if (baseName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return baseName;
            }

            var withoutExt = Path.GetFileNameWithoutExtension(baseName);
            if (string.IsNullOrWhiteSpace(withoutExt))
            {
                withoutExt = $"{documentCode}_{versionNumber}";
            }

            return $"{withoutExt}.pdf";
        }

        private static string EnsureDocxExtension(string? originalFileName, string documentCode, string versionNumber)
        {
            var baseName = string.IsNullOrWhiteSpace(originalFileName)
                ? $"{documentCode}_{versionNumber}"
                : originalFileName.Trim();

            if (baseName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                return baseName;
            }

            var withoutExt = Path.GetFileNameWithoutExtension(baseName);
            if (string.IsNullOrWhiteSpace(withoutExt))
            {
                withoutExt = $"{documentCode}_{versionNumber}";
            }

            return $"{withoutExt}.docx";
        }

        private static string EnsureXlsxExtension(string? originalFileName, string documentCode, string versionNumber)
        {
            var baseName = string.IsNullOrWhiteSpace(originalFileName)
                ? $"{documentCode}_{versionNumber}"
                : originalFileName.Trim();

            if (baseName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return baseName;
            }

            var withoutExt = Path.GetFileNameWithoutExtension(baseName);
            if (string.IsNullOrWhiteSpace(withoutExt))
            {
                withoutExt = $"{documentCode}_{versionNumber}";
            }

            return $"{withoutExt}.xlsx";
        }

        private sealed class DatabaseDocumentFile
        {
            public required string FileName { get; set; }
            public required string OriginalFileName { get; set; }
            public required string FileExtension { get; set; }
            public required string MimeType { get; set; }
            public long FileSize { get; set; }
            public required byte[] FileContent { get; set; }
        }
    }
}
