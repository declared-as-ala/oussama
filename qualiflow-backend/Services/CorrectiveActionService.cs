using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.CorrectiveActions;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;

namespace DocApi.Services
{
    public class CorrectiveActionService : ICorrectiveActionService
    {
        private readonly ICorrectiveActionRepository _correctiveActionRepository;
        private readonly ICorrectiveActionActionLogRepository _correctiveActionActionLogRepository;
        private readonly INonConformityRepository _nonConformityRepository;
        private readonly IUserRepository _userRepository;
        private readonly IDocumentRepository _documentRepository;
        private readonly INotificationEventPublisher _notificationEventPublisher;

        public CorrectiveActionService(
            ICorrectiveActionRepository correctiveActionRepository,
            ICorrectiveActionActionLogRepository correctiveActionActionLogRepository,
            INonConformityRepository nonConformityRepository,
            IUserRepository userRepository,
            IDocumentRepository documentRepository,
            INotificationEventPublisher notificationEventPublisher)
        {
            _correctiveActionRepository = correctiveActionRepository;
            _correctiveActionActionLogRepository = correctiveActionActionLogRepository;
            _nonConformityRepository = nonConformityRepository;
            _userRepository = userRepository;
            _documentRepository = documentRepository;
            _notificationEventPublisher = notificationEventPublisher;
        }

        public async Task<PagedCorrectiveActionResponse> GetCorrectiveActionsAsync(GetCorrectiveActionsQueryRequest query, UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationId = ResolveOrganizationScopeForRead(userContext);

            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);

            await _correctiveActionRepository.SyncOverdueStatusesAsync(organizationId);

            var normalizedStatus = NormalizeStatus(query.Status);
            var normalizedType = NormalizeType(query.Type);

            if (!string.IsNullOrWhiteSpace(normalizedStatus) && !CorrectiveActionConstants.AllowedStatuses.Contains(normalizedStatus))
            {
                throw new ServiceException("Le statut de l'action corrective est invalide.");
            }

            if (!string.IsNullOrWhiteSpace(normalizedType) && !CorrectiveActionConstants.AllowedTypes.Contains(normalizedType))
            {
                throw new ServiceException("Le type d'action corrective est invalide.");
            }

            var isOrgAdminOrQa = string.Equals(userContext.Role, UserRoles.ADMIN_ORG, StringComparison.OrdinalIgnoreCase)
                || string.Equals(userContext.Role, UserRoles.RESPONSABLE_QUALITE, StringComparison.OrdinalIgnoreCase)
                || userContext.IsSuperAdmin;

            int? restrictedUserId = isOrgAdminOrQa ? null : userContext.UserId;

            var items = await _correctiveActionRepository.SearchAsync(
                pageNumber,
                pageSize,
                NormalizeSearch(query.Search),
                normalizedStatus,
                normalizedType,
                query.ResponsibleUserId,
                query.NonConformityId,
                query.IsOverdue,
                query.FromDate,
                query.ToDate,
                organizationId,
                restrictedUserId);

            var total = await _correctiveActionRepository.CountSearchAsync(
                NormalizeSearch(query.Search),
                normalizedStatus,
                normalizedType,
                query.ResponsibleUserId,
                query.NonConformityId,
                query.IsOverdue,
                query.FromDate,
                query.ToDate,
                organizationId,
                restrictedUserId);

            return new PagedCorrectiveActionResponse
            {
                Total = total,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Items = items.Select(MapToListItemResponse).ToList()
            };
        }

        public async Task<CorrectiveActionDetailsResponse> GetByIdAsync(int id, UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationId = ResolveOrganizationScopeForRead(userContext);

            await _correctiveActionRepository.SyncOverdueStatusesAsync(organizationId);

            var details = await _correctiveActionRepository.GetDetailsByIdAsync(id, organizationId);
            if (details == null)
            {
                throw new NotFoundException("Action corrective introuvable.");
            }

            var history = await _correctiveActionActionLogRepository.GetByCorrectiveActionIdAsync(id, organizationId);
            return MapToDetailsResponse(details, history);
        }

        public async Task<CorrectiveActionResponse> CreateAsync(CreateCorrectiveActionRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var organizationId = ResolveOrganizationScopeForWrite(userContext);

            var validated = await ValidatePayloadAsync(
                request.NonConformityId,
                request.Type,
                request.Title,
                request.Description,
                request.ResponsibleUserId,
                request.DueDate,
                request.Status,
                request.ProofRecordId,
                organizationId);

            var completionDate = ResolveCompletionDate(validated.Status, null, null);

            var entity = new CorrectiveAction
            {
                OrganizationId = organizationId,
                NonConformityId = validated.NonConformity.Id,
                Type = validated.Type,
                Title = validated.Title,
                Description = validated.Description,
                ResponsibleUserId = validated.ResponsibleUserId,
                DueDate = validated.DueDate,
                Status = validated.Status,
                CompletionDate = completionDate,
                EffectivenessVerified = null,
                EffectivenessComment = null,
                ProofRecordId = validated.ProofRecordId,
                CreatedAt = DateTime.UtcNow
            };

            var id = await _correctiveActionRepository.CreateAsync(entity);

            await AddActionLogAsync(
                organizationId,
                id,
                actionType: "CORRECTIVE_ACTION_CREATED",
                oldValue: null,
                newValue: entity.Status,
                comment: "Création de l'action corrective.",
                performedByUserId: userContext.UserId);

            await _notificationEventPublisher.PublishToUserAsync(
                organizationId,
                entity.ResponsibleUserId,
                NotificationConstants.TypeCorrectiveActionAssigned,
                NotificationConstants.CategoryInfo,
                $"Action corrective assignee ({validated.NonConformity.Code})",
                $"{entity.Title} vous a ete assignee avec echeance au {entity.DueDate:dd/MM/yyyy}.",
                NotificationConstants.PriorityHigh,
                "CORRECTIVE_ACTION",
                id.ToString(),
                $"/corrective-actions/{id}",
                userContext.UserId);

            var created = await _correctiveActionRepository.GetDetailsByIdAsync(id, organizationId);
            if (created == null)
            {
                throw new NotFoundException("Action corrective introuvable apres creation.");
            }

            return MapToResponse(created);
        }

        public async Task<CorrectiveActionResponse> UpdateAsync(int id, UpdateCorrectiveActionRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var organizationId = ResolveOrganizationScopeForWrite(userContext);

            var current = await GetActionOrThrowAsync(id);
            EnsureAccessToOrganization(current, organizationId);

            var previousStatus = NormalizeStatus(current.Status) ?? CorrectiveActionConstants.StatusPlanned;

            var validated = await ValidatePayloadAsync(
                request.NonConformityId,
                request.Type,
                request.Title,
                request.Description,
                request.ResponsibleUserId,
                request.DueDate,
                request.Status,
                request.ProofRecordId,
                organizationId);

            if (!CorrectiveActionConstants.IsAllowedTransition(previousStatus, validated.Status))
            {
                throw new ServiceException("Transition de statut invalide pour cette action corrective.");
            }

            var nextCompletionDate = ResolveCompletionDate(validated.Status, request.CompletionDate, current.CompletionDate);

            var oldType = current.Type;
            var oldTitle = current.Title;
            var oldDescription = current.Description;
            var oldResponsibleId = current.ResponsibleUserId;
            var oldDueDate = current.DueDate;
            var oldStatus = current.Status;

            current.NonConformityId = validated.NonConformity.Id;
            current.Type = validated.Type;
            current.Title = validated.Title;
            current.Description = validated.Description;
            current.ResponsibleUserId = validated.ResponsibleUserId;
            current.DueDate = validated.DueDate;
            current.Status = validated.Status;
            current.CompletionDate = nextCompletionDate;
            current.ProofRecordId = validated.ProofRecordId;
            current.UpdatedAt = DateTime.UtcNow;

            await _correctiveActionRepository.UpdateAsync(current);

            var changesList = new List<string>();
            if (oldType != current.Type) changesList.Add($"Type : '{oldType}' -> '{current.Type}'");
            if (oldTitle != current.Title) changesList.Add($"Titre : '{oldTitle}' -> '{current.Title}'");
            if (oldDescription != current.Description) changesList.Add($"Description : '{(string.IsNullOrWhiteSpace(oldDescription) ? "aucune" : oldDescription)}' -> '{(string.IsNullOrWhiteSpace(current.Description) ? "aucune" : current.Description)}'");
            if (oldResponsibleId != current.ResponsibleUserId)
            {
                var oldResp = await _userRepository.GetByIdAsync(oldResponsibleId);
                var newResp = await _userRepository.GetByIdAsync(current.ResponsibleUserId);
                changesList.Add($"Responsable : '{oldResp?.FirstName} {oldResp?.LastName}' -> '{newResp?.FirstName} {newResp?.LastName}'");
            }
            if (oldDueDate.Date != current.DueDate.Date) changesList.Add($"Échéance : '{oldDueDate:dd/MM/yyyy}' -> '{current.DueDate:dd/MM/yyyy}'");
            if (oldStatus != current.Status) changesList.Add($"Statut : '{oldStatus}' -> '{current.Status}'");

            if (changesList.Any())
            {
                await AddActionLogAsync(
                    organizationId,
                    current.Id,
                    actionType: "CORRECTIVE_ACTION_UPDATED",
                    oldValue: oldStatus,
                    newValue: current.Status,
                    comment: "Modifications : " + string.Join(" | ", changesList),
                    performedByUserId: userContext.UserId);
            }

            var updated = await _correctiveActionRepository.GetDetailsByIdAsync(id, organizationId);
            if (updated == null)
            {
                throw new NotFoundException("Action corrective introuvable apres mise a jour.");
            }

            return MapToResponse(updated);
        }

        public async Task<bool> DeleteAsync(int id, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var organizationId = ResolveOrganizationScopeForWrite(userContext);

            var action = await GetActionOrThrowAsync(id);
            EnsureAccessToOrganization(action, organizationId);

            var deleted = await _correctiveActionRepository.DeleteAsync(id, organizationId);
            if (deleted)
            {
            }

            return deleted;
        }

        public async Task<CorrectiveActionResponse> UpdateStatusAsync(int id, UpdateCorrectiveActionStatusRequest request, UserContext userContext)
        {
            var action = await GetActionOrThrowAsync(id);
            var organizationId = ResolveOrganizationScopeForWrite(userContext);
            EnsureAccessToOrganization(action, organizationId);

            bool isOrgAdminOrQa = userContext.Role == UserRoles.ADMIN_ORG || userContext.Role == UserRoles.RESPONSABLE_QUALITE;

            if (!isOrgAdminOrQa)
            {
                throw new ForbiddenException("Seul le responsable qualité ou l'administrateur de l'organisation peut modifier la situation de cette action corrective.");
            }

            var nextStatus = NormalizeStatus(request.Status);
            if (string.IsNullOrWhiteSpace(nextStatus) || !CorrectiveActionConstants.AllowedStatuses.Contains(nextStatus))
            {
                throw new ServiceException("Le statut de l'action corrective est invalide.");
            }

            var currentStatus = NormalizeStatus(action.Status) ?? CorrectiveActionConstants.StatusPlanned;
            if (!CorrectiveActionConstants.IsAllowedTransition(currentStatus, nextStatus))
            {
                throw new ServiceException("Transition de statut invalide pour cette action corrective.");
            }

            var nextCompletionDate = ResolveCompletionDate(nextStatus, null, action.CompletionDate);
            if (!CorrectiveActionConstants.IsCompletedStatus(nextStatus))
            {
                nextCompletionDate = null;
            }

            var updatedAt = DateTime.UtcNow;

            await _correctiveActionRepository.UpdateStatusAsync(id, organizationId, nextStatus, nextCompletionDate, updatedAt);
            await AddActionLogAsync(
                organizationId,
                id,
                actionType: "STATUS_CHANGED",
                oldValue: currentStatus,
                newValue: nextStatus,
                comment: NormalizeNullable(request.Comment),
                performedByUserId: userContext.UserId);

            if (nextStatus == CorrectiveActionConstants.StatusCompleted)
            {
                await _notificationEventPublisher.PublishToRolesAsync(
                    organizationId,
                    new List<string> { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE },
                    NotificationConstants.TypeSystemAlert,
                    NotificationConstants.CategorySuccess,
                    "Action corrective réalisée",
                    $"L'action corrective #{id} \"{action.Title}\" a été marquée comme réalisée par son pilote et attend votre vérification d'efficacité.",
                    NotificationConstants.PriorityHigh,
                    "CORRECTIVE_ACTION",
                    id.ToString(),
                    $"/corrective-actions/{id}",
                    userContext.UserId);
            }

            var updated = await _correctiveActionRepository.GetDetailsByIdAsync(id, organizationId);
            if (updated == null)
            {
                throw new NotFoundException("Action corrective introuvable apres mise a jour de statut.");
            }

            return MapToResponse(updated);
        }

        public async Task<CorrectiveActionResponse> VerifyEffectivenessAsync(int id, VerifyCorrectiveActionEffectivenessRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var organizationId = ResolveOrganizationScopeForWrite(userContext);

            var action = await GetActionOrThrowAsync(id);
            EnsureAccessToOrganization(action, organizationId);

            if (!CorrectiveActionConstants.IsCompletedStatus(action.Status))
            {
                throw new ServiceException("La verification d'efficacite est possible uniquement pour une action realisee.");
            }

            if (string.IsNullOrWhiteSpace(request.EffectivenessComment))
            {
                throw new ServiceException("Le commentaire de verification d'efficacite est obligatoire.");
            }

            var updatedAt = DateTime.UtcNow;
            var normalizedComment = request.EffectivenessComment.Trim();

            var currentStatus = NormalizeStatus(action.Status) ?? CorrectiveActionConstants.StatusCompleted;
            var nextStatus = currentStatus;

            if (request.EffectivenessVerified)
            {
                nextStatus = CorrectiveActionConstants.StatusVerified;
            }
            else if (string.Equals(currentStatus, CorrectiveActionConstants.StatusVerified, StringComparison.OrdinalIgnoreCase))
            {
                nextStatus = CorrectiveActionConstants.StatusCompleted;
            }

            if (!string.Equals(currentStatus, nextStatus, StringComparison.OrdinalIgnoreCase))
            {
                if (!CorrectiveActionConstants.IsAllowedTransition(currentStatus, nextStatus))
                {
                    throw new ServiceException("Transition de statut invalide lors de la verification d'efficacite.");
                }

                var completionDate = ResolveCompletionDate(nextStatus, null, action.CompletionDate);
                await _correctiveActionRepository.UpdateStatusAsync(id, organizationId, nextStatus, completionDate, updatedAt);
            }

            await AddActionLogAsync(
                organizationId,
                id,
                actionType: "EFFECTIVENESS_VERIFIED",
                oldValue: currentStatus,
                newValue: nextStatus,
                comment: $"Efficacité vérifiée: {(request.EffectivenessVerified ? "Efficace" : "Non efficace")}. Commentaire: {normalizedComment}",
                performedByUserId: userContext.UserId);

            await _correctiveActionRepository.UpdateEffectivenessAsync(
                id,
                organizationId,
                request.EffectivenessVerified,
                normalizedComment,
                updatedAt);

            var updatedAction = await _correctiveActionRepository.GetDetailsByIdAsync(id, organizationId);
            if (updatedAction == null)
            {
                throw new NotFoundException("Action corrective introuvable apres verification.");
            }

            return MapToResponse(updatedAction);
        }

        public async Task<CorrectiveActionStatisticsResponse> GetStatisticsAsync(UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationId = ResolveOrganizationScopeForRead(userContext);

            await _correctiveActionRepository.SyncOverdueStatusesAsync(organizationId);

            var isOrgAdminOrQa = string.Equals(userContext.Role, UserRoles.ADMIN_ORG, StringComparison.OrdinalIgnoreCase)
                || string.Equals(userContext.Role, UserRoles.RESPONSABLE_QUALITE, StringComparison.OrdinalIgnoreCase)
                || userContext.IsSuperAdmin;

            int? restrictedUserId = isOrgAdminOrQa ? null : userContext.UserId;

            var items = (await _correctiveActionRepository.GetForStatisticsAsync(organizationId)).ToList();
            if (restrictedUserId.HasValue)
            {
                var userNcList = await _nonConformityRepository.SearchAsync(
                    pageNumber: 1,
                    pageSize: 999999,
                    search: null,
                    status: null,
                    severity: null,
                    processId: null,
                    responsibleUserId: null,
                    organizationId: organizationId,
                    restrictedUserId: restrictedUserId.Value);
                var userNcIds = userNcList.Select(nc => nc.Id).ToHashSet();
                items = items.Where(item => userNcIds.Contains(item.NonConformityId)).ToList();
            }
            var today = DateTime.UtcNow.Date;

            var normalizedItems = items.Select(item => new
            {
                Item = item,
                Status = NormalizeStatus(item.Status) ?? CorrectiveActionConstants.StatusPlanned,
                Type = NormalizeType(item.Type) ?? CorrectiveActionConstants.TypeCorrective,
                Responsible = string.IsNullOrWhiteSpace(item.ResponsibleFullName)
                    ? $"User-{item.ResponsibleUserId}"
                    : item.ResponsibleFullName!.Trim(),
                NonConformity = string.IsNullOrWhiteSpace(item.NonConformityCode)
                    ? $"NC-{item.NonConformityId}"
                    : item.NonConformityCode!.Trim()
            }).ToList();

            return new CorrectiveActionStatisticsResponse
            {
                Total = normalizedItems.Count,
                Planned = normalizedItems.Count(x => x.Status == CorrectiveActionConstants.StatusPlanned),
                InProgress = normalizedItems.Count(x => x.Status == CorrectiveActionConstants.StatusInProgress),
                Completed = normalizedItems.Count(x => x.Status == CorrectiveActionConstants.StatusCompleted),
                Verified = normalizedItems.Count(x => x.Status == CorrectiveActionConstants.StatusVerified),
                Overdue = normalizedItems.Count(x => x.Item.DueDate.Date < today && !CorrectiveActionConstants.IsCompletedStatus(x.Status)),
                ByType = normalizedItems
                    .GroupBy(x => x.Type)
                    .ToDictionary(group => group.Key, group => group.Count()),
                ByResponsible = normalizedItems
                    .GroupBy(x => x.Responsible)
                    .ToDictionary(group => group.Key, group => group.Count()),
                ByNonConformity = normalizedItems
                    .GroupBy(x => x.NonConformity)
                    .ToDictionary(group => group.Key, group => group.Count())
            };
        }

        public async Task<List<CorrectiveActionListItemResponse>> GetByNonConformityIdAsync(int nonConformityId, UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationId = ResolveOrganizationScopeForRead(userContext);

            var nonConformity = await _nonConformityRepository.GetByIdAsync(nonConformityId);
            if (nonConformity == null)
            {
                throw new NotFoundException("Non-conformite introuvable.");
            }

            if (nonConformity.OrganizationId != organizationId)
            {
                throw new ForbiddenException("Acces refuse a cette non-conformite.");
            }

            await _correctiveActionRepository.SyncOverdueStatusesAsync(organizationId, nonConformityId);

            var items = await _correctiveActionRepository.GetByNonConformityForListAsync(nonConformityId, organizationId);
            return items.Select(MapToListItemResponse).ToList();
        }

        public async Task<List<CorrectiveActionActionLogResponse>> GetHistoryAsync(int id, UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationId = ResolveOrganizationScopeForRead(userContext);

            var action = await GetActionOrThrowAsync(id);
            EnsureAccessToOrganization(action, organizationId);

            var history = await _correctiveActionActionLogRepository.GetByCorrectiveActionIdAsync(id, organizationId);
            return history.Select(MapToActionLogResponse).ToList();
        }

        public async Task<bool> DeleteActionLogAsync(int logId, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var organizationId = ResolveOrganizationScopeForWrite(userContext);

            var log = await _correctiveActionActionLogRepository.GetByIdAsync(logId, organizationId);
            if (log == null)
            {
                throw new NotFoundException("Log d'actions introuvable.");
            }

            return await _correctiveActionActionLogRepository.DeleteAsync(logId, organizationId);
        }

        private async Task<ValidatedPayload> ValidatePayloadAsync(
            int nonConformityId,
            string type,
            string title,
            string? description,
            int responsibleUserId,
            DateTime dueDate,
            string status,
            int? proofRecordId,
            int organizationId)
        {
            if (nonConformityId <= 0)
            {
                throw new ServiceException("Le rattachement a une non-conformite est obligatoire.");
            }

            var normalizedType = NormalizeType(type);
            if (string.IsNullOrWhiteSpace(normalizedType) || !CorrectiveActionConstants.AllowedTypes.Contains(normalizedType))
            {
                throw new ServiceException("Le type d'action corrective est invalide.");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ServiceException("Le titre de l'action corrective est obligatoire.");
            }

            if (responsibleUserId <= 0)
            {
                throw new ServiceException("Le responsable de l'action corrective est obligatoire.");
            }

            if (dueDate == default)
            {
                throw new ServiceException("La date d'echeance est obligatoire.");
            }

            var normalizedStatus = NormalizeStatus(status);
            if (string.IsNullOrWhiteSpace(normalizedStatus) || !CorrectiveActionConstants.AllowedStatuses.Contains(normalizedStatus))
            {
                throw new ServiceException("Le statut de l'action corrective est invalide.");
            }

            var nonConformity = await _nonConformityRepository.GetByIdAsync(nonConformityId);
            if (nonConformity == null)
            {
                throw new ServiceException("La non-conformite selectionnee est introuvable.");
            }

            if (nonConformity.OrganizationId != organizationId)
            {
                throw new ForbiddenException("La non-conformite doit appartenir a la meme organisation.");
            }

            var responsible = await _userRepository.GetByIdAsync(responsibleUserId);
            if (responsible == null || !responsible.IsActive)
            {
                throw new ServiceException("Le responsable selectionne est invalide ou inactif.");
            }

            if (responsible.OrganizationId != organizationId)
            {
                throw new ForbiddenException("Le responsable doit appartenir a la meme organisation.");
            }

            if (proofRecordId.HasValue)
            {
                var proofRecord = await _documentRepository.GetByIdAsync(proofRecordId.Value);
                if (proofRecord == null)
                {
                    throw new ServiceException("La preuve selectionnee est introuvable.");
                }

                if (proofRecord.OrganizationId != organizationId)
                {
                    throw new ForbiddenException("La preuve doit appartenir a la meme organisation.");
                }

                if (!string.Equals(proofRecord.Type, DocumentConstants.TypeEnregistrement, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ServiceException("La preuve doit etre un document de type ENREGISTREMENT.");
                }
            }

            return new ValidatedPayload
            {
                NonConformity = nonConformity,
                Type = normalizedType,
                Title = title.Trim(),
                Description = NormalizeNullable(description),
                ResponsibleUserId = responsibleUserId,
                DueDate = dueDate,
                Status = normalizedStatus,
                ProofRecordId = proofRecordId
            };
        }

        private async Task AddActionLogAsync(
            int organizationId,
            int actionId,
            string actionType,
            string? oldValue,
            string? newValue,
            string? comment,
            int performedByUserId)
        {
            await _correctiveActionActionLogRepository.CreateAsync(new CorrectiveActionActionLog
            {
                OrganizationId = organizationId,
                CorrectiveActionId = actionId,
                ActionType = actionType,
                OldValue = oldValue,
                NewValue = newValue,
                Comment = NormalizeNullable(comment),
                PerformedByUserId = performedByUserId,
                PerformedAt = DateTime.UtcNow
            });
        }

        private async Task<CorrectiveAction> GetActionOrThrowAsync(int id)
        {
            var entity = await _correctiveActionRepository.GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("Action corrective introuvable.");
            }

            entity.Status = NormalizeStatus(entity.Status) ?? CorrectiveActionConstants.StatusPlanned;
            entity.Type = NormalizeType(entity.Type) ?? CorrectiveActionConstants.TypeCorrective;
            return entity;
        }

        private static DateTime? ResolveCompletionDate(string normalizedStatus, DateTime? requestedCompletionDate, DateTime? currentCompletionDate)
        {
            if (CorrectiveActionConstants.IsCompletedStatus(normalizedStatus))
            {
                return requestedCompletionDate ?? currentCompletionDate ?? DateTime.UtcNow;
            }

            if (requestedCompletionDate.HasValue)
            {
                throw new ServiceException("La date de realisation ne peut etre renseignee que pour une action realisee ou verifiee.");
            }

            return null;
        }

        private static void EnsureCanRead(UserContext userContext)
        {
            if (!userContext.CanReadCorrectiveActions)
            {
                throw new ForbiddenException("Vous n'avez pas les droits de lecture sur les actions correctives.");
            }
        }

        private static void EnsureCanWrite(UserContext userContext)
        {
            if (!userContext.CanWriteCorrectiveActions)
            {
                throw new ForbiddenException("Vous n'avez pas les droits d'ecriture sur les actions correctives.");
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

        private static int ResolveOrganizationScopeForRead(UserContext userContext)
        {
            if (!userContext.OrganizationId.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable dans le token utilisateur.");
            }

            return userContext.OrganizationId.Value;
        }

        private static void EnsureAccessToOrganization(CorrectiveAction action, int organizationId)
        {
            if (action.OrganizationId != organizationId)
            {
                throw new ForbiddenException("Acces refuse a cette action corrective.");
            }
        }

        private static CorrectiveActionListItemResponse MapToListItemResponse(CorrectiveActionListItemData item)
        {
            var normalizedStatus = NormalizeStatus(item.Status) ?? CorrectiveActionConstants.StatusPlanned;
            return new CorrectiveActionListItemResponse
            {
                Id = item.Id,
                NonConformityId = item.NonConformityId,
                NonConformityCode = item.NonConformityCode,
                Type = NormalizeType(item.Type) ?? CorrectiveActionConstants.TypeCorrective,
                Title = item.Title,
                Description = item.Description,
                ResponsibleUserId = item.ResponsibleUserId,
                ResponsibleFullName = item.ResponsibleFullName,
                DueDate = item.DueDate,
                Status = normalizedStatus,
                IsOverdue = IsOverdue(item.DueDate, normalizedStatus),
                CompletionDate = item.CompletionDate,
                CreatedAt = item.CreatedAt
            };
        }

        private static CorrectiveActionResponse MapToResponse(CorrectiveActionDetailsData details)
        {
            var normalizedStatus = NormalizeStatus(details.Status) ?? CorrectiveActionConstants.StatusPlanned;
            return new CorrectiveActionResponse
            {
                Id = details.Id,
                OrganizationId = details.OrganizationId,
                NonConformityId = details.NonConformityId,
                NonConformityCode = details.NonConformityCode,
                Type = NormalizeType(details.Type) ?? CorrectiveActionConstants.TypeCorrective,
                Title = details.Title,
                Description = details.Description,
                ResponsibleUserId = details.ResponsibleUserId,
                ResponsibleFullName = details.ResponsibleFullName,
                DueDate = details.DueDate,
                Status = normalizedStatus,
                CompletionDate = details.CompletionDate,
                EffectivenessVerified = details.EffectivenessVerified,
                EffectivenessComment = details.EffectivenessComment,
                ProofRecordId = details.ProofRecordId,
                IsOverdue = IsOverdue(details.DueDate, normalizedStatus),
                CreatedAt = details.CreatedAt,
                UpdatedAt = details.UpdatedAt
            };
        }

        private static CorrectiveActionDetailsResponse MapToDetailsResponse(
            CorrectiveActionDetailsData details,
            IEnumerable<CorrectiveActionActionLogData> history)
        {
            return new CorrectiveActionDetailsResponse
            {
                Action = MapToResponse(details),
                NonConformity = new CorrectiveActionLinkedNonConformityResponse
                {
                    Id = details.NonConformityId,
                    Code = details.NonConformityCode,
                    Title = details.NonConformityTitle,
                    Description = details.NonConformityDescription
                },
                Responsible = new CorrectiveActionResponsibleResponse
                {
                    Id = details.ResponsibleUserId,
                    FullName = details.ResponsibleFullName,
                    Email = details.ResponsibleEmail
                },
                Proof = details.ProofRecordId.HasValue
                    ? new CorrectiveActionProofRecordResponse
                    {
                        Id = details.ProofRecordId.Value,
                        Code = details.ProofRecordCode,
                        Title = details.ProofRecordTitle,
                        Type = details.ProofRecordType
                    }
                    : null,
                History = history.Select(MapToActionLogResponse).ToList()
            };
        }

        private static CorrectiveActionActionLogResponse MapToActionLogResponse(CorrectiveActionActionLogData history)
        {
            return new CorrectiveActionActionLogResponse
            {
                Id = history.Id,
                ActionType = history.ActionType,
                OldValue = history.OldValue,
                NewValue = history.NewValue,
                Comment = history.Comment,
                PerformedByUserId = history.PerformedByUserId,
                PerformedByFullName = history.PerformedByFullName,
                PerformedAt = history.PerformedAt
            };
        }

        private static bool IsOverdue(DateTime dueDate, string status)
        {
            return dueDate.Date < DateTime.UtcNow.Date && !CorrectiveActionConstants.IsCompletedStatus(status);
        }

        private static string? NormalizeSearch(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static string? NormalizeType(string? value) => CorrectiveActionConstants.NormalizeType(value);

        private static string? NormalizeStatus(string? value) => CorrectiveActionConstants.NormalizeStatus(value);

        private static string? NormalizeNullable(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private sealed class ValidatedPayload
        {
            public required NonConformity NonConformity { get; set; }
            public required string Type { get; set; }
            public required string Title { get; set; }
            public string? Description { get; set; }
            public int ResponsibleUserId { get; set; }
            public DateTime DueDate { get; set; }
            public required string Status { get; set; }
            public int? ProofRecordId { get; set; }
        }
    }
}
