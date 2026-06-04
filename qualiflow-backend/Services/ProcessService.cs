using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.Processes;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;

namespace DocApi.Services
{
    public class ProcessService : IProcessService
    {
        private readonly IProcessRepository _processRepository;
        private readonly IProcessActorRepository _processActorRepository;
        private readonly IUserRepository _userRepository;
        private readonly IProcessActionLogRepository _processActionLogRepository;
        private readonly IActionLogger _actionLogger;
        private readonly IDocumentRepository _documentRepository;

        public ProcessService(
            IProcessRepository processRepository,
            IProcessActorRepository processActorRepository,
            IUserRepository userRepository,
            IProcessActionLogRepository processActionLogRepository,
            IActionLogger actionLogger,
            IDocumentRepository documentRepository)
        {
            _processRepository = processRepository;
            _processActorRepository = processActorRepository;
            _userRepository = userRepository;
            _processActionLogRepository = processActionLogRepository;
            _actionLogger = actionLogger;
            _documentRepository = documentRepository;
        }

        public async Task<PagedProcessResponse> GetProcessesAsync(ProcessListQueryParameters query, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
            var organizationFilter = ResolveOrganizationScopeForRead(userContext, query.OrganizationId);

            int? restrictedUserId = (userContext.Role == UserRoles.UTILISATEUR || query.MyProcessesOnly == true)
                ? userContext.UserId
                : null;

            var processes = await _processRepository.SearchAsync(
                pageNumber,
                pageSize,
                NormalizeSearch(query.Search),
                NormalizeUpper(query.Type),
                NormalizeUpper(query.Status),
                query.PilotUserId,
                organizationFilter,
                restrictedUserId);

            var total = await _processRepository.CountSearchAsync(
                NormalizeSearch(query.Search),
                NormalizeUpper(query.Type),
                NormalizeUpper(query.Status),
                query.PilotUserId,
                organizationFilter,
                restrictedUserId);

            var items = await Task.WhenAll(processes.Select(MapToListItemAsync));

            return new PagedProcessResponse
            {
                Total = total,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Items = items.ToList()
            };
        }

        public async Task<ProcessDetailsResponse> GetByIdAsync(int id, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var process = await GetProcessOrThrowAsync(id);
            EnsureProcessAccess(userContext, process.OrganizationId);
            await EnsureProcessReadAccessAsync(process, userContext);

            var response = await MapToProcessResponseAsync(process);
            var actors = await GetActorsAsync(id, userContext);

            return new ProcessDetailsResponse
            {
                Process = response,
                Actors = actors
            };
        }

        public async Task<ProcessResponse> CreateAsync(CreateProcessRequest request, UserContext userContext, int? organizationId = null)
        {
            EnsureCanWrite(userContext);

            var targetOrganizationId = ResolveOrganizationScopeForWrite(userContext, organizationId);
            await ValidateCreateOrUpdateRequestAsync(request.Code, request.Name, request.Type, request.Status, request.PilotUserId, targetOrganizationId, null);

            var process = new Process
            {
                OrganizationId = targetOrganizationId,
                Code = request.Code.Trim(),
                Name = request.Name.Trim(),
                Description = NormalizeNullable(request.Description),
                Type = request.Type.Trim().ToUpperInvariant(),
                Finalities = SerializeList(request.Finalities),
                Scope = SerializeList(request.Scope),
                Suppliers = SerializeList(request.Suppliers),
                Clients = SerializeList(request.Clients),
                InputData = SerializeList(request.InputData),
                OutputData = SerializeList(request.OutputData),
                Objectives = SerializeList(request.Objectives),
                PilotUserId = request.PilotUserId,
                Status = string.IsNullOrWhiteSpace(request.Status)
                    ? ProcessConstants.StatusActif
                    : request.Status.Trim().ToUpperInvariant(),
                VersionNumber = string.IsNullOrWhiteSpace(request.VersionNumber) ? "1.0" : request.VersionNumber.Trim(),
                RevisionComment = string.IsNullOrWhiteSpace(request.RevisionComment) ? "CrÃƒÂ©ation initiale" : request.RevisionComment.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            var id = await _processRepository.CreateAsync(process);
            var created = await GetProcessOrThrowAsync(id);

            await SyncPilotInActorsAsync(created.Id, created.OrganizationId, created.PilotUserId);

            await LogProcessActionAsync(
                created,
                "PROCESS_CREATED",
                null,
                created.Code,
                $"Processus crÃƒÂ©ÃƒÂ© : '{created.Name}'.",
                userContext.UserId);

            return await MapToProcessResponseAsync(created);
        }

        public async Task<ProcessResponse> UpdateAsync(int id, UpdateProcessRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var process = await GetProcessOrThrowAsync(id);
            EnsureProcessWriteAccess(userContext, process.OrganizationId);
            await VerifyWritePermissionAsync(process, userContext);

            // Seuls ADMIN_ORG, RESPONSABLE_QUALITE et SUPER_ADMIN peuvent changer le pilote
            var canChangePilot = userContext.IsSuperAdmin
                || userContext.Role == UserRoles.ADMIN_ORG
                || userContext.Role == UserRoles.RESPONSABLE_QUALITE;
            var effectivePilotId = canChangePilot ? request.PilotUserId : process.PilotUserId;

            await ValidateCreateOrUpdateRequestAsync(
                request.Code,
                request.Name,
                request.Type,
                request.Status,
                effectivePilotId,
                process.OrganizationId,
                id);

            var oldCode = process.Code;
            var oldName = process.Name;
            var oldDescription = process.Description;
            var oldType = process.Type;
            var oldStatus = process.Status;
            var oldPilotId = process.PilotUserId;
            var oldObjectivesSer = process.Objectives;
            var oldFinalitiesSer = process.Finalities;
            var oldScopeSer = process.Scope;
            var oldSuppliersSer = process.Suppliers;
            var oldClientsSer = process.Clients;
            var oldInputDataSer = process.InputData;
            var oldOutputDataSer = process.OutputData;

            var oldVersionNumber = process.VersionNumber;
            var oldRevisionComment = process.RevisionComment;

            process.Code = request.Code.Trim();
            process.Name = request.Name.Trim();
            process.Description = NormalizeNullable(request.Description);
            process.Type = request.Type.Trim().ToUpperInvariant();
            process.Finalities = SerializeList(request.Finalities);
            process.Scope = SerializeList(request.Scope);
            process.Suppliers = SerializeList(request.Suppliers);
            process.Clients = SerializeList(request.Clients);
            process.InputData = SerializeList(request.InputData);
            process.OutputData = SerializeList(request.OutputData);
            process.Objectives = SerializeList(request.Objectives);
            process.PilotUserId = effectivePilotId;
            process.Status = string.IsNullOrWhiteSpace(request.Status)
                ? ProcessConstants.StatusActif
                : request.Status.Trim().ToUpperInvariant();
            if (decimal.TryParse(oldVersionNumber, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedVer))
            {
                process.VersionNumber = (parsedVer + 0.1m).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                process.VersionNumber = oldVersionNumber;
            }
            process.RevisionComment = request.RevisionComment?.Trim();
            process.UpdatedAt = DateTime.UtcNow;

            await _processRepository.UpdateAsync(process);

            var changesList = new List<string>();
            if (oldCode != process.Code) changesList.Add($"Code : '{oldCode}' Ã¢â€ â€™ '{process.Code}'");
            if (oldName != process.Name) changesList.Add($"Nom : '{oldName}' Ã¢â€ â€™ '{process.Name}'");
            if (oldDescription != process.Description) changesList.Add($"Description : '{(string.IsNullOrWhiteSpace(oldDescription) ? "aucune" : oldDescription)}' Ã¢â€ â€™ '{(string.IsNullOrWhiteSpace(process.Description) ? "aucune" : process.Description)}'");
            if (oldType != process.Type) changesList.Add($"Type : '{oldType}' Ã¢â€ â€™ '{process.Type}'");
            if (oldStatus != process.Status) changesList.Add($"Statut : '{oldStatus}' Ã¢â€ â€™ '{process.Status}'");
            if (oldVersionNumber != process.VersionNumber) changesList.Add($"Version : '{oldVersionNumber}' Ã¢â€ â€™ '{process.VersionNumber}'");
            if (oldRevisionComment != process.RevisionComment) changesList.Add($"Commentaire : '{oldRevisionComment}' Ã¢â€ â€™ '{process.RevisionComment}'");
            
            if (oldPilotId != process.PilotUserId)
            {
                var oldPilotName = await GetPilotFullNameAsync(oldPilotId) ?? "aucun";
                var newPilotName = await GetPilotFullNameAsync(process.PilotUserId) ?? "aucun";
                changesList.Add($"Pilote : '{oldPilotName}' Ã¢â€ â€™ '{newPilotName}'");
            }

            var oldObjectives = DeserializeList(oldObjectivesSer);
            await SyncPilotInActorsAsync(process.Id, process.OrganizationId, process.PilotUserId);
            var newObjectives = DeserializeList(process.Objectives);
            if (!oldObjectives.SequenceEqual(newObjectives))
            {
                changesList.Add($"Objectifs : '{(oldObjectives.Any() ? string.Join(", ", oldObjectives) : "aucun")}' Ã¢â€ â€™ '{(newObjectives.Any() ? string.Join(", ", newObjectives) : "aucun")}'");
            }

            var oldFinalities = DeserializeList(oldFinalitiesSer);
            var newFinalities = DeserializeList(process.Finalities);
            if (!oldFinalities.SequenceEqual(newFinalities))
            {
                changesList.Add($"FinalitÃƒÂ©s : '{(oldFinalities.Any() ? string.Join(", ", oldFinalities) : "aucune")}' Ã¢â€ â€™ '{(newFinalities.Any() ? string.Join(", ", newFinalities) : "aucune")}'");
            }

            var oldScope = DeserializeList(oldScopeSer);
            var newScope = DeserializeList(process.Scope);
            if (!oldScope.SequenceEqual(newScope))
            {
                changesList.Add($"PÃƒÂ©rimÃƒÂ¨tre : '{(oldScope.Any() ? string.Join(", ", oldScope) : "aucun")}' Ã¢â€ â€™ '{(newScope.Any() ? string.Join(", ", newScope) : "aucun")}'");
            }

            var detailedComment = changesList.Any()
                ? "Modifications : " + string.Join(" | ", changesList)
                : "MÃƒÂ©tadonnÃƒÂ©es du processus modifiÃƒÂ©es sans changement de contenu.";

            await LogProcessActionAsync(
                process,
                "PROCESS_UPDATED",
                $"Code: {oldCode}, Nom: {oldName}, Type: {oldType}, Statut: {oldStatus}, Pilote: {oldPilotId}",
                $"Code: {process.Code}, Nom: {process.Name}, Type: {process.Type}, Statut: {process.Status}, Pilote: {process.PilotUserId}",
                detailedComment,
                userContext.UserId);

            return await MapToProcessResponseAsync(process);
        }

        public async Task<bool> DeleteAsync(int id, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var process = await GetProcessOrThrowAsync(id);
            EnsureProcessWriteAccess(userContext, process.OrganizationId);
            if (!userContext.IsSuperAdmin && userContext.Role != UserRoles.ADMIN_ORG && userContext.Role != UserRoles.RESPONSABLE_QUALITE)
            {
                throw new ForbiddenException("Le pilote de processus n'est pas autorisé à supprimer un processus. Seuls les administrateurs ou les responsables qualité le peuvent.");
            }

            var result = await _processRepository.DeleteAsync(id);
            if (result)
            {
                await LogProcessActionAsync(
                    process,
                    "PROCESS_DELETED",
                    process.Code,
                    null,
                    $"Processus '{process.Name}' supprimÃƒÂ©.",
                    userContext.UserId);
            }
            return result;
        }

        public async Task<ProcessResponse> ToggleStatusAsync(int id, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var process = await GetProcessOrThrowAsync(id);
            EnsureProcessWriteAccess(userContext, process.OrganizationId);
            await VerifyWritePermissionAsync(process, userContext);

            var nextStatus = process.Status == ProcessConstants.StatusActif
                ? ProcessConstants.StatusInactif
                : ProcessConstants.StatusActif;

            var prevStatus = process.Status;
            await _processRepository.ToggleStatusAsync(id, nextStatus);
            process.Status = nextStatus;
            process.UpdatedAt = DateTime.UtcNow;

            await LogProcessActionAsync(
                process,
                "STATUS_TOGGLED",
                prevStatus,
                nextStatus,
                $"Statut changÃƒÂ© de {prevStatus} ÃƒÂ  {nextStatus}.",
                userContext.UserId);

            return await MapToProcessResponseAsync(process);
        }

        public async Task<ProcessResponse> UpdatePilotAsync(int id, UpdateProcessPilotRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var process = await GetProcessOrThrowAsync(id);
            EnsureProcessWriteAccess(userContext, process.OrganizationId);
            await VerifyWritePermissionAsync(process, userContext);

            await ValidatePilotAsync(request.PilotUserId, process.OrganizationId);

            var oldPilotId = process.PilotUserId;
            var oldPilotName = await GetPilotFullNameAsync(oldPilotId) ?? "Aucun";

            process.PilotUserId = request.PilotUserId;
            process.UpdatedAt = DateTime.UtcNow;
            await _processRepository.UpdateAsync(process);
            await SyncPilotInActorsAsync(process.Id, process.OrganizationId, request.PilotUserId);


            var newPilotName = await GetPilotFullNameAsync(request.PilotUserId) ?? "Aucun";

            await LogProcessActionAsync(
                process,
                "PILOT_UPDATED",
                $"ID: {oldPilotId}, Nom: {oldPilotName}",
                $"ID: {request.PilotUserId}, Nom: {newPilotName}",
                $"Pilote mis ÃƒÂ  jour : de '{oldPilotName}' ÃƒÂ  '{newPilotName}'.",
                userContext.UserId);

            return await MapToProcessResponseAsync(process);
        }

        public async Task<List<ProcessActorResponse>> GetActorsAsync(int processId, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var process = await GetProcessOrThrowAsync(processId);
            EnsureProcessAccess(userContext, process.OrganizationId);
            await EnsureProcessReadAccessAsync(process, userContext);

            var actors = await _processActorRepository.GetActorsByProcessIdAsync(processId);
            return actors.Select(MapActorToResponse).ToList();
        }

        public async Task<List<ProcessActorResponse>> AssignActorsAsync(int processId, AssignProcessActorsRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var process = await GetProcessOrThrowAsync(processId);
            EnsureProcessWriteAccess(userContext, process.OrganizationId);
            await VerifyWritePermissionAsync(process, userContext);

            if (request?.Actors == null)
            {
                throw new ServiceException("La liste des acteurs est invalide.");
            }

            var normalizedActors = request.Actors
                .GroupBy(actor => actor.UserId)
                .Select(group => group.First())
                .ToList();

            // Align the actors list with the process's main pilot to maintain domain model consistency
            if (process.PilotUserId.HasValue)
            {
                // Demote any other user with role PILOTE to COPILOTE to maintain the single-pilot constraint without failing
                foreach (var actor in normalizedActors)
                {
                    if (string.Equals(NormalizeUpper(actor.ActorType), ProcessConstants.ActorPilote, StringComparison.OrdinalIgnoreCase)
                        && actor.UserId != process.PilotUserId.Value)
                    {
                        actor.ActorType = ProcessConstants.ActorCopilote;
                    }
                }

                // Ensure the main pilot is present in the actors list with the PILOTE role
                var mainPilotActor = normalizedActors.FirstOrDefault(a => a.UserId == process.PilotUserId.Value);
                if (mainPilotActor != null)
                {
                    mainPilotActor.ActorType = ProcessConstants.ActorPilote;
                }
                else
                {
                    normalizedActors.Add(new AssignProcessActorItemRequest
                    {
                        UserId = process.PilotUserId.Value,
                        ActorType = ProcessConstants.ActorPilote
                    });
                }
            }
            else
            {
                // Demote any PILOTE actor if the process has no main pilot
                foreach (var actor in normalizedActors)
                {
                    if (string.Equals(NormalizeUpper(actor.ActorType), ProcessConstants.ActorPilote, StringComparison.OrdinalIgnoreCase))
                    {
                        actor.ActorType = ProcessConstants.ActorCopilote;
                    }
                }
            }

            var oldActors = (await _processActorRepository.GetActorsByProcessIdAsync(processId)).ToList();
            var protectedProcedurePilots = oldActors
                .Where(actor => string.Equals(actor.ActorType, ProcessConstants.ActorPiloteProcedure, StringComparison.OrdinalIgnoreCase))
                .Where(actor => normalizedActors.All(requestedActor => requestedActor.UserId != actor.UserId))
                .Select(actor => new AssignProcessActorItemRequest
                {
                    UserId = actor.UserId,
                    ActorType = ProcessConstants.ActorPiloteProcedure
                });

            normalizedActors.AddRange(protectedProcedurePilots);

            // Also preserve RESPONSABLE_INDICATEUR actors that are not explicitly in the request
            var protectedIndicatorResponsibles = oldActors
                .Where(actor => string.Equals(actor.ActorType, ProcessConstants.ActorResponsableIndicateur, StringComparison.OrdinalIgnoreCase))
                .Where(actor => normalizedActors.All(requestedActor => requestedActor.UserId != actor.UserId))
                .Select(actor => new AssignProcessActorItemRequest
                {
                    UserId = actor.UserId,
                    ActorType = ProcessConstants.ActorResponsableIndicateur
                });

            normalizedActors.AddRange(protectedIndicatorResponsibles);

            foreach (var actor in normalizedActors)
            {
                if (actor.UserId <= 0)
                {
                    throw new ServiceException("Chaque acteur doit avoir un identifiant utilisateur valide.");
                }

                var actorType = NormalizeUpper(actor.ActorType);
                if (string.IsNullOrWhiteSpace(actorType) || !ProcessConstants.AllowedActorTypes.Contains(actorType))
                {
                    throw new ServiceException("Type d'acteur invalide.");
                }

                var user = await _userRepository.GetByIdAsync(actor.UserId);
                if (user == null || !user.IsActive)
                {
                    throw new ServiceException($"Utilisateur acteur invalide: {actor.UserId}");
                }

                if (user.OrganizationId != process.OrganizationId)
                {
                    throw new ForbiddenException("Les acteurs doivent appartenir a la meme organisation que le processus.");
                }
            }

            var pilotActors = normalizedActors.Where(a => string.Equals(NormalizeUpper(a.ActorType), ProcessConstants.ActorPilote, StringComparison.OrdinalIgnoreCase)).ToList();
            if (pilotActors.Count > 1)
            {
                throw new ServiceException("Un processus ne peut avoir qu'un seul pilote dans la liste des acteurs.");
            }
            if (pilotActors.Count == 1 && process.PilotUserId.HasValue)
            {
                if (pilotActors[0].UserId != process.PilotUserId.Value)
                {
                    throw new ServiceException("Le pilote dÃƒÂ©fini dans la liste des acteurs doit correspondre au pilote principal du processus.");
                }
            }

            var oldActorsListStr = string.Join(", ", oldActors.Select(a => $"{a.FirstName} {a.LastName} ({a.ActorType})"));

            var now = DateTime.UtcNow;
            var actorEntities = normalizedActors.Select(actor => new ProcessActor
            {
                OrganizationId = process.OrganizationId,
                ProcessId = process.Id,
                UserId = actor.UserId,
                ActorType = NormalizeUpper(actor.ActorType)!,
                AssignedAt = now
            });

            await _processActorRepository.ReplaceActorsAsync(processId, process.OrganizationId, actorEntities);

            var newActors = await _processActorRepository.GetActorsByProcessIdAsync(processId);
            var newActorsListStr = string.Join(", ", newActors.Select(a => $"{a.FirstName} {a.LastName} ({a.ActorType})"));

            var oldActorsDict = oldActors.ToDictionary(a => a.UserId, a => a);
            var newActorsDict = newActors.ToDictionary(a => a.UserId, a => a);
            var actorChanges = new List<string>();

            // Added/updated actors
            foreach (var newActor in newActors)
            {
                var actorFullName = $"{newActor.FirstName} {newActor.LastName}".Trim();
                if (!oldActorsDict.TryGetValue(newActor.UserId, out var oldActor))
                {
                    actorChanges.Add($"L'acteur '{actorFullName}' a ÃƒÂ©tÃƒÂ© ajoutÃƒÂ© avec le rÃƒÂ´le '{TranslateActorType(newActor.ActorType)}'");
                }
                else if (oldActor.ActorType != newActor.ActorType)
                {
                    actorChanges.Add($"Le rÃƒÂ´le de l'acteur '{actorFullName}' a changÃƒÂ© de '{TranslateActorType(oldActor.ActorType)}' ÃƒÂ  '{TranslateActorType(newActor.ActorType)}'");
                }
            }

            // Removed actors
            foreach (var oldActor in oldActors)
            {
                if (!newActorsDict.ContainsKey(oldActor.UserId))
                {
                    var actorFullName = $"{oldActor.FirstName} {oldActor.LastName}".Trim();
                    actorChanges.Add($"L'acteur '{actorFullName}' a ÃƒÂ©tÃƒÂ© retirÃƒÂ© (rÃƒÂ´le prÃƒÂ©cÃƒÂ©dent : '{TranslateActorType(oldActor.ActorType)}')");
                }
            }

            var detailedComment = actorChanges.Any()
                ? "Changements d'acteurs : " + string.Join(" | ", actorChanges)
                : "Acteurs enregistrÃƒÂ©s sans changement.";

            await LogProcessActionAsync(
                process,
                "ACTORS_ASSIGNED",
                oldActorsListStr,
                newActorsListStr,
                detailedComment,
                userContext.UserId);

            return await GetActorsAsync(processId, userContext);
        }

        public async Task<bool> RemoveActorAsync(int processId, int userId, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var process = await GetProcessOrThrowAsync(processId);
            EnsureProcessWriteAccess(userContext, process.OrganizationId);
            await VerifyWritePermissionAsync(process, userContext);

            if (process.PilotUserId == userId)
            {
                throw new ServiceException("Le pilote principal du processus ne peut pas Ãªtre retirÃ© de la liste des acteurs. Veuillez modifier le pilote dans les dÃ©tails du processus.");
            }

            var currentActors = await _processActorRepository.GetActorsByProcessIdAsync(processId);
            var actorToRemove = currentActors.FirstOrDefault(actor => actor.UserId == userId);
            if (actorToRemove != null && string.Equals(actorToRemove.ActorType, ProcessConstants.ActorPiloteProcedure, StringComparison.OrdinalIgnoreCase))
            {
                throw new ServiceException("Le pilote de procedure est lie au responsable de procedure et ne peut pas etre retire depuis les acteurs du processus.");
            }

            if (actorToRemove != null && string.Equals(actorToRemove.ActorType, ProcessConstants.ActorResponsableIndicateur, StringComparison.OrdinalIgnoreCase))
            {
                throw new ServiceException("Le responsable d'indicateur est lié à un indicateur actif et ne peut pas être retiré depuis les acteurs du processus.");
            }

            var actorUser = await _userRepository.GetByIdAsync(userId);
            var actorName = actorUser != null ? $"{actorUser.FirstName} {actorUser.LastName}".Trim() : $"ID: {userId}";

            var removed = await _processActorRepository.RemoveActorAsync(processId, userId);
            if (removed)
            {
                await LogProcessActionAsync(
                    process,
                    "ACTOR_REMOVED",
                    actorName,
                    null,
                    $"Acteur '{actorName}' retirÃƒÂ© du processus.",
                    userContext.UserId);
            }

            return removed;
        }

        public async Task<ProcessMapResponse> GetMapAsync(UserContext userContext)
        {
            EnsureCanRead(userContext);

            var organizationScope = userContext.IsSuperAdmin ? (int?)null : userContext.OrganizationId;
            if (!userContext.IsSuperAdmin && !organizationScope.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable dans le token utilisateur.");
            }

            int? restrictedUserId = (userContext.Role == UserRoles.UTILISATEUR)
                ? userContext.UserId
                : null;

            var processes = await _processRepository.GetByOrganizationAsync(organizationScope, restrictedUserId);
            var listItems = await Task.WhenAll(processes.Select(MapToListItemAsync));

            return new ProcessMapResponse
            {
                PilotageProcesses = listItems.Where(p => p.Type == ProcessConstants.TypePilotage).ToList(),
                RealisationProcesses = listItems.Where(p => p.Type == ProcessConstants.TypeRealisation).ToList(),
                SupportProcesses = listItems.Where(p => p.Type == ProcessConstants.TypeSupport).ToList()
            };
        }

        public async Task<ProcessStatisticsResponse> GetStatisticsAsync(UserContext userContext)
        {
            EnsureCanRead(userContext);

            var organizationScope = userContext.IsSuperAdmin ? (int?)null : userContext.OrganizationId;
            if (!userContext.IsSuperAdmin && !organizationScope.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable dans le token utilisateur.");
            }

            int? restrictedUserId = (userContext.Role == UserRoles.UTILISATEUR)
                ? userContext.UserId
                : null;

            var processes = (await _processRepository.GetByOrganizationAsync(organizationScope, restrictedUserId)).ToList();

            return new ProcessStatisticsResponse
            {
                Total = processes.Count,
                Active = processes.Count(p => p.Status == ProcessConstants.StatusActif),
                Inactive = processes.Count(p => p.Status == ProcessConstants.StatusInactif),
                ByType = new Dictionary<string, int>
                {
                    [ProcessConstants.TypePilotage] = processes.Count(p => p.Type == ProcessConstants.TypePilotage),
                    [ProcessConstants.TypeRealisation] = processes.Count(p => p.Type == ProcessConstants.TypeRealisation),
                    [ProcessConstants.TypeSupport] = processes.Count(p => p.Type == ProcessConstants.TypeSupport)
                },
                WithPilot = processes.Count(p => p.PilotUserId.HasValue),
                WithoutPilot = processes.Count(p => !p.PilotUserId.HasValue)
            };
        }

        private async Task ValidateCreateOrUpdateRequestAsync(
            string code,
            string name,
            string type,
            string status,
            int? pilotUserId,
            int organizationId,
            int? existingProcessId)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ServiceException("Le code du processus est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ServiceException("Le nom du processus est obligatoire.");
            }

            var normalizedType = NormalizeUpper(type);
            if (string.IsNullOrWhiteSpace(normalizedType) || !ProcessConstants.AllowedTypes.Contains(normalizedType))
            {
                throw new ServiceException("Le type de processus est invalide.");
            }

            var normalizedStatus = NormalizeUpper(status);
            if (string.IsNullOrWhiteSpace(normalizedStatus) || !ProcessConstants.AllowedStatuses.Contains(normalizedStatus))
            {
                throw new ServiceException("Le statut du processus est invalide.");
            }

            var codeExists = await _processRepository.ExistsCodeAsync(organizationId, code.Trim(), existingProcessId);
            if (codeExists)
            {
                throw new ServiceException("Ce code de processus existe deja pour l'organisation.");
            }

            await ValidatePilotAsync(pilotUserId, organizationId);
        }

        private async Task ValidatePilotAsync(int? pilotUserId, int organizationId)
        {
            if (!pilotUserId.HasValue)
            {
                return;
            }

            var pilot = await _userRepository.GetByIdAsync(pilotUserId.Value);
            if (pilot == null || !pilot.IsActive)
            {
                throw new ServiceException("Le pilote selectionne est invalide ou inactif.");
            }

            if (pilot.OrganizationId != organizationId)
            {
                throw new ForbiddenException("Le pilote doit appartenir a la meme organisation que le processus.");
            }
        }

        private async Task<Process> GetProcessOrThrowAsync(int id)
        {
            var process = await _processRepository.GetByIdAsync(id);
            if (process == null)
            {
                throw new NotFoundException("Processus introuvable.");
            }

            return process;
        }

        private static int ResolveOrganizationScopeForWrite(UserContext userContext, int? explicitOrganizationId)
        {
            if (userContext.IsSuperAdmin)
            {
                if (explicitOrganizationId.HasValue)
                {
                    return explicitOrganizationId.Value;
                }

                if (userContext.OrganizationId.HasValue)
                {
                    return userContext.OrganizationId.Value;
                }

                throw new ServiceException("SUPER_ADMIN doit preciser organizationId pour creer un processus.");
            }

            if (!userContext.OrganizationId.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable dans le token utilisateur.");
            }

            if (explicitOrganizationId.HasValue && explicitOrganizationId.Value != userContext.OrganizationId.Value)
            {
                throw new ForbiddenException("Acces refuse a l'organisation demandee.");
            }

            return userContext.OrganizationId.Value;
        }

        private static int? ResolveOrganizationScopeForRead(UserContext userContext, int? requestedOrganizationId)
        {
            if (userContext.IsSuperAdmin)
            {
                return requestedOrganizationId;
            }

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

        private static void EnsureCanRead(UserContext userContext)
        {
            if (!userContext.CanReadProcesses)
            {
                throw new ForbiddenException("Vous n'avez pas les droits de lecture sur les processus.");
            }
        }

        private static void EnsureCanWrite(UserContext userContext)
        {
            if (!userContext.CanWriteProcesses)
            {
                throw new ForbiddenException("Vous n'avez pas les droits d'ecriture sur les processus.");
            }
        }

        private static void EnsureProcessAccess(UserContext userContext, int organizationId)
        {
            if (userContext.IsSuperAdmin)
            {
                return;
            }

            if (!userContext.OrganizationId.HasValue || userContext.OrganizationId.Value != organizationId)
            {
                throw new ForbiddenException("Acces refuse a ce processus.");
            }
        }

        private static void EnsureProcessWriteAccess(UserContext userContext, int organizationId)
        {
            EnsureCanWrite(userContext);
            EnsureProcessAccess(userContext, organizationId);
        }

        private async Task VerifyWritePermissionAsync(Process process, UserContext userContext)
        {
            if (userContext.IsSuperAdmin || userContext.Role == UserRoles.RESPONSABLE_QUALITE || userContext.Role == UserRoles.ADMIN_ORG)
            {
                return;
            }

            if (userContext.Role == UserRoles.UTILISATEUR)
            {
                if (process.PilotUserId == userContext.UserId)
                {
                    return;
                }

                var actors = await _processActorRepository.GetActorsByProcessIdAsync(process.Id);
                var userActor = actors.FirstOrDefault(a => a.UserId == userContext.UserId);
                if (userActor != null)
                {
                    var type = userActor.ActorType.Trim().ToUpperInvariant();
                    if (type == ProcessConstants.ActorPilote || type == ProcessConstants.ActorCopilote)
                    {
                        return;
                    }
                }

                throw new ForbiddenException("Seul le pilote ou le co-pilote peut modifier ce processus et gerer ses acteurs.");
            }
        }

        private async Task EnsureProcessReadAccessAsync(Process process, UserContext userContext)
        {
            if (userContext.IsSuperAdmin || userContext.Role == UserRoles.ADMIN_ORG || userContext.Role == UserRoles.RESPONSABLE_QUALITE)
            {
                return;
            }

            if (userContext.Role == UserRoles.UTILISATEUR)
            {
                if (process.PilotUserId == userContext.UserId)
                {
                    return;
                }

                var actors = await _processActorRepository.GetActorsByProcessIdAsync(process.Id);
                if (actors.Any(a => a.UserId == userContext.UserId))
                {
                    return;
                }

                throw new ForbiddenException("Acces refuse. Vous n'etes pas acteur de ce processus.");
            }
        }

        private async Task<ProcessListItemResponse> MapToListItemAsync(Process process)
        {
            return new ProcessListItemResponse
            {
                Id = process.Id,
                Code = process.Code,
                Name = process.Name,
                Type = process.Type,
                Status = process.Status,
                PilotUserId = process.PilotUserId,
                PilotFullName = await GetPilotFullNameAsync(process.PilotUserId),
                OrganizationId = process.OrganizationId,
                VersionNumber = process.VersionNumber,
                CreatedAt = process.CreatedAt
            };
        }

        private async Task<ProcessResponse> MapToProcessResponseAsync(Process process)
        {
            return new ProcessResponse
            {
                Id = process.Id,
                OrganizationId = process.OrganizationId,
                Code = process.Code,
                Name = process.Name,
                Description = process.Description,
                Type = process.Type,
                Finalities = DeserializeList(process.Finalities),
                Scope = DeserializeList(process.Scope),
                Suppliers = DeserializeList(process.Suppliers),
                Clients = DeserializeList(process.Clients),
                InputData = DeserializeList(process.InputData),
                OutputData = DeserializeList(process.OutputData),
                Objectives = DeserializeList(process.Objectives),
                PilotUserId = process.PilotUserId,
                PilotFullName = await GetPilotFullNameAsync(process.PilotUserId),
                Status = process.Status,
                VersionNumber = process.VersionNumber,
                RevisionComment = process.RevisionComment,
                CreatedAt = process.CreatedAt,
                UpdatedAt = process.UpdatedAt
            };
        }

        private async Task<string?> GetPilotFullNameAsync(int? pilotUserId)
        {
            if (!pilotUserId.HasValue)
            {
                return null;
            }

            var pilot = await _userRepository.GetByIdAsync(pilotUserId.Value);
            if (pilot == null)
            {
                return null;
            }

            return $"{pilot.FirstName} {pilot.LastName}".Trim();
        }

        private static ProcessActorResponse MapActorToResponse(ProcessActorDetails actor)
        {
            return new ProcessActorResponse
            {
                UserId = actor.UserId,
                FullName = $"{actor.FirstName} {actor.LastName}".Trim(),
                Email = actor.Email,
                Function = actor.Function,
                ActorType = actor.ActorType,
                AssignedAt = actor.AssignedAt
            };
        }

        private static string SerializeList(IEnumerable<string>? values)
        {
            var normalized = values?
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            return JsonSerializer.Serialize(normalized);
        }

        private static List<string> DeserializeList(string? serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return new List<string>();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(serialized);
                if (parsed == null)
                {
                    return new List<string>();
                }

                return parsed
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim())
                    .ToList();
            }
            catch
            {
                return serialized
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => v.Trim())
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList();
            }
        }

        private static string? NormalizeSearch(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        public async Task<List<ProcessActionLogResponse>> GetActionLogsAsync(int processId, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var process = await GetProcessOrThrowAsync(processId);
            EnsureProcessAccess(userContext, process.OrganizationId);

            var logs = await _processActionLogRepository.GetByProcessIdAsync(processId, process.OrganizationId);
            return logs.Select(MapToActionLogResponse).ToList();
        }

        private async Task LogProcessActionAsync(
            Process process,
            string actionType,
            string? oldValue,
            string? newValue,
            string? comment,
            int performedByUserId)
        {
            await _processActionLogRepository.CreateAsync(new ProcessActionLog
            {
                OrganizationId = process.OrganizationId,
                ProcessId = process.Id,
                ActionType = actionType,
                OldValue = oldValue,
                NewValue = newValue,
                Comment = comment,
                PerformedByUserId = performedByUserId,
                PerformedAt = DateTime.UtcNow
            });

            try
            {
                var user = await _userRepository.GetByIdAsync(performedByUserId);
                var actorName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : "SystÃƒÂ¨me";
                await _actionLogger.LogActionAsync(
                    process.OrganizationId,
                    performedByUserId,
                    actorName,
                    "PROCESS",
                    actionType.Replace("PROCESS_", ""),
                    $"Processus {process.Code} : {actionType}",
                    comment ?? $"Action {actionType} effectuÃƒÂ©e sur le processus '{process.Name}'.");
            }
            catch
            {
                // Ignored to avoid breaking primary database operations if logger fails
            }
        }

        private static ProcessActionLogResponse MapToActionLogResponse(ProcessActionLogData log)
        {
            return new ProcessActionLogResponse
            {
                Id = log.Id,
                OrganizationId = log.OrganizationId,
                ProcessId = log.ProcessId,
                ActionType = log.ActionType,
                OldValue = log.OldValue,
                NewValue = log.NewValue,
                Comment = log.Comment,
                PerformedByUserId = log.PerformedByUserId,
                PerformedByFullName = log.PerformedByFullName,
                PerformedAt = log.PerformedAt
            };
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

        private static string TranslateActorType(string actorType)
        {
            switch (actorType?.Trim().ToUpperInvariant())
            {
                case "PILOTE": return "Pilote";
                case "PILOTE_PROCEDURE": return "Pilote procedure";
                case "COPILOTE": return "Co-pilote";
                case "ACTEUR": return "Acteur";
                case "OBSERVATEUR": return "Observateur";
                default: return actorType ?? "Inconnu";
            }
        }

        public async Task<bool> DeleteActionLogAsync(int logId, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            if (!userContext.OrganizationId.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable dans le token utilisateur.");
            }

            var log = await _processActionLogRepository.GetByIdAsync(logId, userContext.OrganizationId.Value);
            if (log == null)
            {
                throw new NotFoundException("Journal d'actions introuvable.");
            }

            var process = await GetProcessOrThrowAsync(log.ProcessId);
            EnsureProcessWriteAccess(userContext, process.OrganizationId);
            await VerifyWritePermissionAsync(process, userContext);

            return await _processActionLogRepository.DeleteAsync(logId, userContext.OrganizationId.Value);
        }
        private async Task SyncPilotInActorsAsync(int processId, int organizationId, int? newPilotUserId)
        {
            var currentActors = await _processActorRepository.GetActorsByProcessIdAsync(processId);
            var updatedActors = currentActors.Where(a => !string.Equals(a.ActorType, ProcessConstants.ActorPilote, StringComparison.OrdinalIgnoreCase)).ToList();

            if (newPilotUserId.HasValue)
            {
                updatedActors = updatedActors.Where(a => a.UserId != newPilotUserId.Value).ToList();
                var newPilot = new DocApi.Domain.Entities.ProcessActorDetails 
                { 
                    UserId = newPilotUserId.Value, 
                    ActorType = ProcessConstants.ActorPilote,
                    FirstName = string.Empty,
                    LastName = string.Empty,
                    Email = string.Empty
                };
                updatedActors.Add(newPilot);
            }

            var now = DateTime.UtcNow;
            var actorEntities = updatedActors.Select(actor => new ProcessActor
            {
                OrganizationId = organizationId,
                ProcessId = processId,
                UserId = actor.UserId,
                ActorType = actor.ActorType.ToUpperInvariant(),
                AssignedAt = now
            });

            await _processActorRepository.ReplaceActorsAsync(processId, organizationId, actorEntities);
        }

        public async Task<bool> AddDocumentLinkAsync(int processId, int documentId, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var process = await GetProcessOrThrowAsync(processId);
            EnsureProcessWriteAccess(userContext, process.OrganizationId);
            await VerifyWritePermissionAsync(process, userContext);

            var doc = await _documentRepository.GetByIdAsync(documentId);
            if (doc == null || doc.OrganizationId != process.OrganizationId)
            {
                throw new NotFoundException("Document introuvable ou n'appartient pas à la même organisation.");
            }

            var result = await _documentRepository.AddProcessLinkAsync(documentId, processId);
            if (result)
            {
                await LogProcessActionAsync(
                    process,
                    "DOCUMENT_LINKED",
                    null,
                    $"Doc ID: {documentId}",
                    $"Document ID {documentId} lié au processus.",
                    userContext.UserId);
            }
            return result;
        }

        public async Task<bool> RemoveDocumentLinkAsync(int processId, int documentId, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var process = await GetProcessOrThrowAsync(processId);
            EnsureProcessWriteAccess(userContext, process.OrganizationId);
            await VerifyWritePermissionAsync(process, userContext);

            var doc = await _documentRepository.GetByIdAsync(documentId);
            if (doc == null || doc.OrganizationId != process.OrganizationId)
            {
                throw new NotFoundException("Document introuvable.");
            }

            var result = await _documentRepository.RemoveProcessLinkAsync(documentId, processId);
            if (result)
            {
                await LogProcessActionAsync(
                    process,
                    "DOCUMENT_UNLINKED",
                    $"Doc ID: {documentId}",
                    null,
                    $"Document ID {documentId} délié du processus.",
                    userContext.UserId);
            }
            return result;
        }
    }
}



