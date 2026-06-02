using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.NonConformities;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;

namespace DocApi.Services
{
    public class NonConformityService : INonConformityService
    {
        private readonly INonConformityRepository _nonConformityRepository;
        private readonly ICorrectiveActionRepository _correctiveActionRepository;
        private readonly IProcessRepository _processRepository;
        private readonly IProcedureRepository _procedureRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationEventPublisher _notificationEventPublisher;
        private readonly IActionLogger _actionLogger;

        public NonConformityService(
            INonConformityRepository nonConformityRepository,
            ICorrectiveActionRepository correctiveActionRepository,
            IProcessRepository processRepository,
            IProcedureRepository procedureRepository,
            IUserRepository userRepository,
            INotificationEventPublisher notificationEventPublisher,
            IActionLogger actionLogger)
        {
            _nonConformityRepository = nonConformityRepository;
            _correctiveActionRepository = correctiveActionRepository;
            _processRepository = processRepository;
            _procedureRepository = procedureRepository;
            _userRepository = userRepository;
            _notificationEventPublisher = notificationEventPublisher;
            _actionLogger = actionLogger;
        }

        public async Task<PagedNonConformityResponse> GetNonConformitiesAsync(NonConformityListQueryParameters query, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
            var organizationScope = ResolveOrganizationScopeForRead(userContext, query.OrganizationId);

            await _correctiveActionRepository.SyncOverdueStatusesAsync(organizationScope);

            var isOrgAdminOrQa = string.Equals(userContext.Role, UserRoles.ADMIN_ORG, StringComparison.OrdinalIgnoreCase)
                || string.Equals(userContext.Role, UserRoles.RESPONSABLE_QUALITE, StringComparison.OrdinalIgnoreCase)
                || userContext.IsSuperAdmin;

            int? restrictedUserId = isOrgAdminOrQa ? null : userContext.UserId;

            var items = await _nonConformityRepository.SearchAsync(
                pageNumber,
                pageSize,
                NormalizeSearch(query.Search),
                NormalizeUpper(query.Status),
                NormalizeUpper(query.Severity),
                query.ProcessId,
                query.ResponsibleUserId,
                organizationScope,
                restrictedUserId);

            var total = await _nonConformityRepository.CountSearchAsync(
                NormalizeSearch(query.Search),
                NormalizeUpper(query.Status),
                NormalizeUpper(query.Severity),
                query.ProcessId,
                query.ResponsibleUserId,
                organizationScope,
                restrictedUserId);

            return new PagedNonConformityResponse
            {
                Total = total,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Items = items.Select(MapToListItemResponse).ToList()
            };
        }

        public async Task<PagedNonConformityResponse> GetAwaitingValidationAsync(NonConformityListQueryParameters query, UserContext userContext)
        {
            EnsureCanRead(userContext);

            // Only quality managers and admins can view awaiting validation list
            if (!string.Equals(userContext.Role, UserRoles.RESPONSABLE_QUALITE, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(userContext.Role, UserRoles.ADMIN_ORG, StringComparison.OrdinalIgnoreCase)
                && !userContext.IsSuperAdmin)
            {
                throw new ForbiddenException("Vous n'avez pas les permissions pour accéder à cette liste.");
            }

            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
            var organizationIdScope = ResolveOrganizationScopeForRead(userContext, query.OrganizationId);

            if (!organizationIdScope.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable.");
            }

            var items = await _nonConformityRepository.GetAwaitingValidationAsync(
                organizationIdScope.Value,
                pageNumber,
                pageSize);

            var total = await _nonConformityRepository.CountAwaitingValidationAsync(organizationIdScope.Value);

            return new PagedNonConformityResponse
            {
                Total = total,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Items = items.Select(MapToListItemResponse).ToList()
            };
        }

        public async Task<NonConformityDetailsResponse> GetByIdAsync(int id, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var nonConformity = await GetNonConformityOrThrowAsync(id);
            EnsureAccessToOrganization(userContext, nonConformity.OrganizationId);

            await _correctiveActionRepository.SyncOverdueStatusesAsync(nonConformity.OrganizationId, id);

            var response = await MapToNonConformityResponseAsync(nonConformity);
            var actions = await _correctiveActionRepository.GetByNonConformityIdAsync(id);
            var attachments = await _nonConformityRepository.GetAttachmentsByNonConformityIdAsync(id);

            return new NonConformityDetailsResponse
            {
                NonConformity = response,
                Actions = actions.Select(MapToCorrectiveActionResponse).ToList(),
                Attachments = attachments.Select(MapToAttachmentResponse).ToList()
            };
        }

        public async Task<NonConformityResponse> CreateAsync(CreateNonConformityRequest request, UserContext userContext)
        {
            EnsureCanCreate(userContext);

            var organizationId = ResolveOrganizationScopeForWrite(userContext);

            var isExempt = userContext.Role == UserRoles.ADMIN_ORG
                        || userContext.Role == UserRoles.RESPONSABLE_QUALITE
                        || userContext.IsSuperAdmin;

            if (!isExempt)
            {
                var userProcesses = await _processRepository.GetByOrganizationAsync(organizationId, userContext.UserId);
                if (userProcesses == null || !userProcesses.Any())
                {
                    throw new ForbiddenException("Vous devez être affecté à au moins un processus pour créer une non-conformité.");
                }
            }

            await ValidateNonConformityPayloadAsync(
                request.Code,
                request.Title,
                request.Type,
                request.Severity,
                request.Status,
                request.ProcessId,
                request.ProcedureId,
                request.ResponsibleUserId,
                organizationId,
                null,
                requireValidationData: false);

            // Generate code if not provided
            var code = string.IsNullOrWhiteSpace(request.Code)
                ? await _nonConformityRepository.GenerateNextCodeAsync(organizationId)
                : request.Code.Trim();

            // UTILISATEUR always creates NCs in "awaiting validation" state.
            // Only ADMIN_ORG and RESPONSABLE_QUALITE can set a custom status at creation.
            var isPrivilegedCreator = userContext.Role == UserRoles.ADMIN_ORG
                                   || userContext.Role == UserRoles.RESPONSABLE_QUALITE;

            var resolvedStatus = isPrivilegedCreator && !string.IsNullOrWhiteSpace(request.Status)
                ? request.Status.Trim().ToUpperInvariant()
                : NonConformityConstants.StatusEnAttenteValidation;

            var entity = new NonConformity
            {
                OrganizationId = organizationId,
                Code = code,
                Title = request.Title.Trim(),
                Description = NormalizeNullable(request.Description),
                Type = request.Type.Trim().ToUpperInvariant(),
                Severity = request.Severity.Trim().ToUpperInvariant(),
                ProcessId = request.ProcessId,
                ProcedureId = request.ProcedureId,
                DetectedDate = request.DetectedDate == default ? DateTime.UtcNow : request.DetectedDate,
                ResponsibleUserId = request.ResponsibleUserId,
                Status = resolvedStatus,
                CreatedAt = DateTime.UtcNow
            };


            var id = await _nonConformityRepository.CreateAsync(entity);
            var created = await GetNonConformityOrThrowAsync(id);
            await PublishNonConformityCreatedEventsAsync(created, userContext.UserId);

            await _actionLogger.LogActionAsync(
                organizationId,
                userContext.UserId,
                $"{userContext.FirstName} {userContext.LastName}",
                "NON_CONFORMITY",
                "CREATE",
                $"Création NC {created.Code}",
                $"La non-conformité '{created.Title}' a été créée.");

            return await MapToNonConformityResponseAsync(created);
        }

        public async Task<NonConformityResponse> UpdateAsync(int id, UpdateNonConformityRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var entity = await GetNonConformityOrThrowAsync(id);
            EnsureWriteAccessToOrganization(userContext, entity.OrganizationId);

            await ValidateNonConformityPayloadAsync(
                request.Code,
                request.Title,
                request.Type,
                request.Severity,
                request.Status,
                request.ProcessId,
                request.ProcedureId,
                request.ResponsibleUserId,
                entity.OrganizationId,
                id,
                requireValidationData: true);

            entity.Code = NormalizeNullable(request.Code);
            entity.Title = request.Title.Trim();
            entity.Description = NormalizeNullable(request.Description);
            entity.Type = request.Type.Trim().ToUpperInvariant();
            entity.Severity = request.Severity.Trim().ToUpperInvariant();
            entity.ProcessId = request.ProcessId;
            entity.ProcedureId = request.ProcedureId;
            entity.DetectedDate = request.DetectedDate == default ? entity.DetectedDate : request.DetectedDate;
            entity.ResponsibleUserId = request.ResponsibleUserId;
            entity.Status = string.IsNullOrWhiteSpace(request.Status)
                ? NonConformityConstants.StatusEnAttenteValidation
                : request.Status.Trim().ToUpperInvariant();
            entity.UpdatedAt = DateTime.UtcNow;

            await _nonConformityRepository.UpdateAsync(entity);

            return await MapToNonConformityResponseAsync(entity);
        }

        public async Task<NonConformityResponse> ValidateAsync(int id, ValidateNonConformityRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var entity = await GetNonConformityOrThrowAsync(id);
            EnsureWriteAccessToOrganization(userContext, entity.OrganizationId);

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                throw new ServiceException("Le code de la non-conformite est obligatoire pour la validation.");
            }

            if (request.ResponsibleUserId <= 0)
            {
                throw new ServiceException("Le responsable du traitement est obligatoire pour la validation.");
            }

            var trimmedCode = request.Code.Trim();
            if (await _nonConformityRepository.ExistsCodeAsync(entity.OrganizationId, trimmedCode, id))
            {
                throw new ServiceException("Ce code de non-conformite existe deja dans l'organisation.");
            }

            await ValidateResponsibleAsync(request.ResponsibleUserId, entity.OrganizationId);
            await _nonConformityRepository.ValidateAsync(id, trimmedCode, request.ResponsibleUserId, NonConformityConstants.StatusOuverte);

            entity.Code = trimmedCode;
            entity.ResponsibleUserId = request.ResponsibleUserId;
            entity.Status = NonConformityConstants.StatusOuverte;
            entity.UpdatedAt = DateTime.UtcNow;

            await _notificationEventPublisher.PublishToUserAsync(
                entity.OrganizationId,
                request.ResponsibleUserId,
                NotificationConstants.TypeSystemAlert,
                NotificationConstants.CategoryInfo,
                $"Non-conformite {entity.Code}: traitement assigne",
                $"La non-conformite {entity.Code} a ete validee et vous a ete assignee pour traitement.",
                NotificationConstants.PriorityMedium,
                "NON_CONFORMITY",
                entity.Id.ToString(),
                $"/non-conformities/{entity.Id}",
                userContext.UserId);

            var listItem = await _nonConformityRepository.GetListItemByIdAsync(entity.Id);

            await _actionLogger.LogActionAsync(
                entity.OrganizationId,
                userContext.UserId,
                $"{userContext.FirstName} {userContext.LastName}",
                "NON_CONFORMITY",
                "VALIDATE",
                $"Validation NC {entity.Code}",
                $"La non-conformité a été validée et assignée à {listItem?.ResponsibleFullName}.");

            return await MapToNonConformityResponseAsync(entity);
        }

        public async Task<bool> DeleteAsync(int id, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var entity = await GetNonConformityOrThrowAsync(id);
            EnsureWriteAccessToOrganization(userContext, entity.OrganizationId);

            var deleted = await _nonConformityRepository.DeleteAsync(id);
            if (deleted)
            {
            }

            return deleted;
        }

        public async Task<NonConformityResponse> UpdateStatusAsync(int id, UpdateNonConformityStatusRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var entity = await GetNonConformityOrThrowAsync(id);
            EnsureWriteAccessToOrganization(userContext, entity.OrganizationId);

            var normalizedStatus = NormalizeUpper(request.Status);
            if (string.IsNullOrWhiteSpace(normalizedStatus) || !NonConformityConstants.AllowedStatuses.Contains(normalizedStatus))
            {
                throw new ServiceException("Le statut de la non-conformite est invalide.");
            }

            await _nonConformityRepository.UpdateStatusAsync(id, normalizedStatus);
            entity.Status = normalizedStatus;
            entity.UpdatedAt = DateTime.UtcNow;
            if (entity.ResponsibleUserId.HasValue)
            {
                await _notificationEventPublisher.PublishToUserAsync(
                    entity.OrganizationId,
                    entity.ResponsibleUserId.Value,
                    NotificationConstants.TypeSystemAlert,
                    NotificationConstants.CategoryInfo,
                    $"Non-conformite {entity.Code ?? entity.Id.ToString()}: statut mis a jour",
                    $"Le statut de la non-conformite {entity.Code ?? entity.Id.ToString()} est maintenant {normalizedStatus}.",
                    NotificationConstants.PriorityMedium,
                    "NON_CONFORMITY",
                    entity.Id.ToString(),
                    $"/non-conformities/{entity.Id}",
                    userContext.UserId);
            }

            await _actionLogger.LogActionAsync(
                entity.OrganizationId,
                userContext.UserId,
                $"{userContext.FirstName} {userContext.LastName}",
                "NON_CONFORMITY",
                "STATUS_UPDATE",
                $"Statut NC {entity.Code} -> {normalizedStatus}",
                $"Le statut de la non-conformité a été mis à jour.");

            return await MapToNonConformityResponseAsync(entity);
        }

        private async Task PublishNonConformityCreatedEventsAsync(NonConformity entity, int triggeredByUserId)
        {
            if (entity.ResponsibleUserId.HasValue)
            {
                await _notificationEventPublisher.PublishToUserAsync(
                    entity.OrganizationId,
                    entity.ResponsibleUserId.Value,
                    NotificationConstants.TypeNonConformityCreated,
                    NotificationConstants.CategoryInfo,
                    $"Nouvelle non-conformite {entity.Code ?? "a valider"}",
                    $"{entity.Title} a ete creee et vous a ete assignee.",
                    NotificationConstants.PriorityMedium,
                    "NON_CONFORMITY",
                    entity.Id.ToString(),
                    $"/non-conformities/{entity.Id}",
                    triggeredByUserId);
            }

            await _notificationEventPublisher.PublishToRolesAsync(
                entity.OrganizationId,
                new[] { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE, UserRoles.UTILISATEUR },
                NotificationConstants.TypeNonConformityCreated,
                NotificationConstants.CategoryInfo,
                $"Nouvelle non-conformite {entity.Code ?? "a valider"}",
                $"{entity.Title} a ete creee et attend validation qualite.",
                NotificationConstants.PriorityMedium,
                "NON_CONFORMITY",
                entity.Id.ToString(),
                $"/non-conformities/{entity.Id}",
                triggeredByUserId);

            if (string.Equals(entity.Severity, NonConformityConstants.SeverityCritique, StringComparison.OrdinalIgnoreCase))
            {
                await _notificationEventPublisher.PublishToRolesAsync(
                    entity.OrganizationId,
                    new[] { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE, UserRoles.UTILISATEUR },
                    NotificationConstants.TypeNonConformityCritical,
                    NotificationConstants.CategoryError,
                    $"Non-conformite critique {entity.Code ?? "a valider"}",
                    $"{entity.Title} est marquee critique. Intervention immediate requise.",
                    NotificationConstants.PriorityCritical,
                    "NON_CONFORMITY",
                    entity.Id.ToString(),
                    $"/non-conformities/{entity.Id}",
                    triggeredByUserId);
            }
        }

        public async Task<NonConformityStatisticsResponse> GetStatisticsAsync(UserContext userContext)
        {
            EnsureCanRead(userContext);

            var organizationScope = ResolveOrganizationScopeForRead(userContext, null)
                ?? throw new ForbiddenException("Organisation introuvable dans le token utilisateur.");

            await _correctiveActionRepository.SyncOverdueStatusesAsync(organizationScope);

            var isOrgAdminOrQa = string.Equals(userContext.Role, UserRoles.ADMIN_ORG, StringComparison.OrdinalIgnoreCase)
                || string.Equals(userContext.Role, UserRoles.RESPONSABLE_QUALITE, StringComparison.OrdinalIgnoreCase)
                || userContext.IsSuperAdmin;

            int? restrictedUserId = isOrgAdminOrQa ? null : userContext.UserId;

            var nonConformities = (await _nonConformityRepository.GetByOrganizationAsync(organizationScope)).ToList();
            if (restrictedUserId.HasValue)
            {
                var userProcesses = await _processRepository.SearchAsync(
                    pageNumber: 1,
                    pageSize: 999999,
                    search: null,
                    type: null,
                    status: null,
                    pilotUserId: null,
                    organizationId: organizationScope,
                    restrictedUserId: restrictedUserId.Value);
                var processIds = userProcesses.Select(p => p.Id).ToHashSet();
                nonConformities = nonConformities.Where(nc => nc.ProcessId.HasValue && processIds.Contains(nc.ProcessId.Value)).ToList();
            }

            var overdueActions = await _correctiveActionRepository.CountOverdueAsync(organizationScope, restrictedUserId);

            return new NonConformityStatisticsResponse
            {
                Total = nonConformities.Count,
                PendingValidation = nonConformities.Count(nc => nc.Status == NonConformityConstants.StatusEnAttenteValidation),
                Opened = nonConformities.Count(nc => nc.Status == NonConformityConstants.StatusOuverte),
                InProgress = nonConformities.Count(nc => nc.Status == NonConformityConstants.StatusEnCours),
                Closed = nonConformities.Count(nc => nc.Status == NonConformityConstants.StatusCloturee),
                Critical = nonConformities.Count(nc => nc.Severity == NonConformityConstants.SeverityCritique),
                OverdueActions = overdueActions,
                BySeverity = new Dictionary<string, int>
                {
                    [NonConformityConstants.SeverityMineure] = nonConformities.Count(nc => nc.Severity == NonConformityConstants.SeverityMineure),
                    [NonConformityConstants.SeverityMajeure] = nonConformities.Count(nc => nc.Severity == NonConformityConstants.SeverityMajeure),
                    [NonConformityConstants.SeverityCritique] = nonConformities.Count(nc => nc.Severity == NonConformityConstants.SeverityCritique)
                },
                ByStatus = new Dictionary<string, int>
                {
                    [NonConformityConstants.StatusEnAttenteValidation] = nonConformities.Count(nc => nc.Status == NonConformityConstants.StatusEnAttenteValidation),
                    [NonConformityConstants.StatusOuverte] = nonConformities.Count(nc => nc.Status == NonConformityConstants.StatusOuverte),
                    [NonConformityConstants.StatusEnCours] = nonConformities.Count(nc => nc.Status == NonConformityConstants.StatusEnCours),
                    [NonConformityConstants.StatusCloturee] = nonConformities.Count(nc => nc.Status == NonConformityConstants.StatusCloturee)
                }
            };
        }

        private async Task ValidateNonConformityPayloadAsync(
            string? code,
            string title,
            string type,
            string severity,
            string status,
            int? processId,
            int? procedureId,
            int? responsibleUserId,
            int organizationId,
            int? excludeId,
            bool requireValidationData)
        {
            var normalizedStatus = NormalizeUpper(status);
            var needsValidatedFields = requireValidationData
                || !string.Equals(normalizedStatus, NonConformityConstants.StatusEnAttenteValidation, StringComparison.OrdinalIgnoreCase);

            if (needsValidatedFields && string.IsNullOrWhiteSpace(code))
            {
                throw new ServiceException("Le code de la non-conformite est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ServiceException("Le titre de la non-conformite est obligatoire.");
            }

            var normalizedType = NormalizeUpper(type);
            if (string.IsNullOrWhiteSpace(normalizedType) || !NonConformityConstants.AllowedTypes.Contains(normalizedType))
            {
                throw new ServiceException("Le type de non-conformite est invalide.");
            }

            var normalizedSeverity = NormalizeUpper(severity);
            if (string.IsNullOrWhiteSpace(normalizedSeverity) || !NonConformityConstants.AllowedSeverities.Contains(normalizedSeverity))
            {
                throw new ServiceException("La severite est invalide.");
            }

            if (string.IsNullOrWhiteSpace(normalizedStatus) || !NonConformityConstants.AllowedStatuses.Contains(normalizedStatus))
            {
                throw new ServiceException("Le statut de la non-conformite est invalide.");
            }

            if (needsValidatedFields && (!responsibleUserId.HasValue || responsibleUserId.Value <= 0))
            {
                throw new ServiceException("Le responsable de la non-conformite est obligatoire.");
            }

            if (!string.IsNullOrWhiteSpace(code))
            {
                var codeExists = await _nonConformityRepository.ExistsCodeAsync(organizationId, code.Trim(), excludeId);
                if (codeExists)
                {
                    throw new ServiceException("Ce code de non-conformite existe deja dans l'organisation.");
                }
            }

            if (responsibleUserId.HasValue && responsibleUserId.Value > 0)
            {
                await ValidateResponsibleAsync(responsibleUserId.Value, organizationId);
            }
            await ValidateProcessAndProcedureAsync(processId, procedureId, organizationId);
        }

        private async Task ValidateResponsibleAsync(int responsibleUserId, int organizationId)
        {
            var responsible = await _userRepository.GetByIdAsync(responsibleUserId);
            if (responsible == null || !responsible.IsActive)
            {
                throw new ServiceException("Le responsable selectionne est invalide ou inactif.");
            }

            if (responsible.OrganizationId != organizationId)
            {
                throw new ForbiddenException("Le responsable doit appartenir a la meme organisation.");
            }
        }

        private async Task ValidateProcessAndProcedureAsync(int? processId, int? procedureId, int organizationId)
        {
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
                    throw new ServiceException("La procedure selectionnee n'appartient pas au processus choisi.");
                }
            }
        }

        private async Task<NonConformity> GetNonConformityOrThrowAsync(int id)
        {
            var entity = await _nonConformityRepository.GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("Non-conformite introuvable.");
            }

            return entity;
        }

        private async Task<NonConformityResponse> MapToNonConformityResponseAsync(NonConformity entity)
        {
            var listItem = await _nonConformityRepository.GetListItemByIdAsync(entity.Id);
            var process = entity.ProcessId.HasValue ? await _processRepository.GetByIdAsync(entity.ProcessId.Value) : null;
            var procedure = entity.ProcedureId.HasValue ? await _procedureRepository.GetByIdAsync(entity.ProcedureId.Value) : null;

            return new NonConformityResponse
            {
                Id = entity.Id,
                OrganizationId = entity.OrganizationId,
                Code = entity.Code,
                Title = entity.Title,
                Description = entity.Description,
                Type = entity.Type,
                Severity = entity.Severity,
                ProcessId = entity.ProcessId,
                ProcessCode = listItem?.ProcessCode,
                ProcessName = process?.Name,
                ProcedureId = entity.ProcedureId,
                ProcedureCode = listItem?.ProcedureCode,
                ProcedureTitle = procedure?.Title,
                ResponsibleUserId = entity.ResponsibleUserId,
                ResponsibleFullName = listItem?.ResponsibleFullName,
                DetectedDate = entity.DetectedDate,
                Status = entity.Status,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        private static NonConformityListItemResponse MapToListItemResponse(NonConformityListItemData item)
        {
            return new NonConformityListItemResponse
            {
                Id = item.Id,
                OrganizationId = item.OrganizationId,
                Code = item.Code,
                Title = item.Title,
                Type = item.Type,
                Severity = item.Severity,
                ProcessId = item.ProcessId,
                ProcessCode = item.ProcessCode,
                ProcedureId = item.ProcedureId,
                ProcedureCode = item.ProcedureCode,
                ResponsibleUserId = item.ResponsibleUserId,
                ResponsibleFullName = item.ResponsibleFullName,
                DetectedDate = item.DetectedDate,
                Status = item.Status,
                CreatedAt = item.CreatedAt
            };
        }

        private static CorrectiveActionResponse MapToCorrectiveActionResponse(CorrectiveActionData action)
        {
            var normalizedStatus = CorrectiveActionConstants.NormalizeStatus(action.Status) ?? CorrectiveActionConstants.StatusPlanned;
            var isOverdue = !action.CompletionDate.HasValue
                && action.DueDate.Date < DateTime.UtcNow.Date
                && !CorrectiveActionConstants.IsCompletedStatus(normalizedStatus);

            return new CorrectiveActionResponse
            {
                Id = action.Id,
                OrganizationId = action.OrganizationId,
                NonConformityId = action.NonConformityId,
                Title = action.Title,
                Description = action.Description,
                ResponsibleUserId = action.ResponsibleUserId,
                ResponsibleFullName = action.ResponsibleFullName,
                DueDate = action.DueDate,
                CompletionDate = action.CompletionDate,
                Status = normalizedStatus,
                IsOverdue = isOverdue,
                CreatedAt = action.CreatedAt,
                UpdatedAt = action.UpdatedAt
            };
        }

        private static void EnsureCanRead(UserContext userContext)
        {
            if (!userContext.CanReadNonConformities)
            {
                throw new ForbiddenException("Vous n'avez pas les droits de lecture sur les non-conformites.");
            }
        }

        private static void EnsureCanWrite(UserContext userContext)
        {
            if (!userContext.CanWriteNonConformities)
            {
                throw new ForbiddenException("Vous n'avez pas les droits d'ecriture sur les non-conformites.");
            }
        }

        private static void EnsureCanCreate(UserContext userContext)
        {
            if (!userContext.CanCreateNonConformities)
            {
                throw new ForbiddenException("Vous n'avez pas les droits de creation sur les non-conformites.");
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

        private static void EnsureAccessToOrganization(UserContext userContext, int organizationId)
        {
            if (!userContext.OrganizationId.HasValue || userContext.OrganizationId.Value != organizationId)
            {
                throw new ForbiddenException("Acces refuse a cette non-conformite.");
            }
        }

        private static void EnsureWriteAccessToOrganization(UserContext userContext, int organizationId)
        {
            EnsureCanWrite(userContext);
            EnsureAccessToOrganization(userContext, organizationId);
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

        private static string? NormalizeNullable(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        public async Task<NonConformityAttachmentResponse> AddAttachmentAsync(int nonConformityId, string originalFileName, string mimeType, byte[] content, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var entity = await GetNonConformityOrThrowAsync(nonConformityId);
            EnsureWriteAccessToOrganization(userContext, entity.OrganizationId);

            var fileExtension = System.IO.Path.GetExtension(originalFileName);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

            var attachment = new NonConformityAttachment
            {
                NonConformityId = nonConformityId,
                OrganizationId = entity.OrganizationId,
                FileName = uniqueFileName,
                OriginalFileName = originalFileName,
                FileExtension = fileExtension,
                MimeType = mimeType,
                FileSize = content.Length,
                FileContent = content,
                CreatedAt = DateTime.UtcNow
            };

            var id = await _nonConformityRepository.AddAttachmentAsync(attachment);
            attachment.Id = id;

            await _actionLogger.LogActionAsync(
                entity.OrganizationId,
                userContext.UserId,
                $"{userContext.FirstName} {userContext.LastName}",
                "NON_CONFORMITY",
                "UPLOAD_ATTACHMENT",
                $"Ajout pièce jointe NC {entity.Code ?? entity.Id.ToString()}",
                $"Le fichier '{originalFileName}' a été téléversé pour la non-conformité.");

            return MapToAttachmentResponse(attachment);
        }

        public async Task<NonConformityAttachment?> GetAttachmentContentAsync(int attachmentId, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var attachment = await _nonConformityRepository.GetAttachmentByIdAsync(attachmentId);
            if (attachment == null)
            {
                throw new NotFoundException("Pièce jointe introuvable.");
            }

            EnsureAccessToOrganization(userContext, attachment.OrganizationId);
            return attachment;
        }

        public async Task<bool> DeleteAttachmentAsync(int attachmentId, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var attachment = await _nonConformityRepository.GetAttachmentByIdAsync(attachmentId);
            if (attachment == null)
            {
                throw new NotFoundException("Pièce jointe introuvable.");
            }

            EnsureWriteAccessToOrganization(userContext, attachment.OrganizationId);

            var deleted = await _nonConformityRepository.DeleteAttachmentAsync(attachmentId);
            if (deleted)
            {
                await _actionLogger.LogActionAsync(
                    attachment.OrganizationId,
                    userContext.UserId,
                    $"{userContext.FirstName} {userContext.LastName}",
                    "NON_CONFORMITY",
                    "DELETE_ATTACHMENT",
                    $"Suppression pièce jointe ID {attachmentId}",
                    $"Le fichier '{attachment.OriginalFileName}' a été supprimé.");
            }

            return deleted;
        }

        private static NonConformityAttachmentResponse MapToAttachmentResponse(NonConformityAttachment item)
        {
            return new NonConformityAttachmentResponse
            {
                Id = item.Id,
                NonConformityId = item.NonConformityId,
                OrganizationId = item.OrganizationId,
                OriginalFileName = item.OriginalFileName,
                FileExtension = item.FileExtension,
                MimeType = item.MimeType,
                FileSize = item.FileSize,
                CreatedAt = item.CreatedAt
            };
        }
    }
}
