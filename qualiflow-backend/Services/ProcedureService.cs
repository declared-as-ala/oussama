using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.Procedures;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;

namespace DocApi.Services
{
    public class ProcedureService : IProcedureService
    {
        private readonly IProcedureRepository _procedureRepository;
        private readonly IInstructionRepository _instructionRepository;
        private readonly IProcessRepository _processRepository;
        private readonly IUserRepository _userRepository;
        private readonly IProcessActorRepository _processActorRepository;
        private readonly IProcedureActionLogRepository _procedureActionLogRepository;
        private readonly IActionLogger _actionLogger;
        private readonly IDocumentRepository _documentRepository;

        public ProcedureService(
            IProcedureRepository procedureRepository,
            IInstructionRepository instructionRepository,
            IProcessRepository processRepository,
            IUserRepository userRepository,
            IProcessActorRepository processActorRepository,
            IProcedureActionLogRepository procedureActionLogRepository,
            IActionLogger actionLogger,
            IDocumentRepository documentRepository)
        {
            _procedureRepository = procedureRepository;
            _instructionRepository = instructionRepository;
            _processRepository = processRepository;
            _userRepository = userRepository;
            _processActorRepository = processActorRepository;
            _procedureActionLogRepository = procedureActionLogRepository;
            _actionLogger = actionLogger;
            _documentRepository = documentRepository;
        }

        public async Task<PagedProcedureResponse> GetProceduresAsync(ProcedureListQueryParameters query, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
            var organizationScope = ResolveOrganizationScopeForRead(userContext, query.OrganizationId);

            int? restrictedUserId = (userContext.Role == UserRoles.UTILISATEUR)
                ? userContext.UserId
                : null;

            var items = await _procedureRepository.SearchAsync(
                pageNumber,
                pageSize,
                NormalizeSearch(query.Search),
                query.ProcessId,
                NormalizeUpper(query.Status),
                query.ResponsibleUserId,
                organizationScope,
                restrictedUserId);

            var total = await _procedureRepository.CountSearchAsync(
                NormalizeSearch(query.Search),
                query.ProcessId,
                NormalizeUpper(query.Status),
                query.ResponsibleUserId,
                organizationScope,
                restrictedUserId);

            return new PagedProcedureResponse
            {
                Total = total,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Items = items.Select(MapToListItemResponse).ToList()
            };
        }

        public async Task<ProcedureDetailsResponse> GetByIdAsync(int id, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var procedure = await GetProcedureOrThrowAsync(id);
            EnsureProcedureReadAccess(userContext, procedure.OrganizationId);

            if (userContext.Role == UserRoles.UTILISATEUR)
            {
                var process = await _processRepository.GetByIdAsync(procedure.ProcessId);
                if (process == null)
                {
                    throw new ForbiddenException("Acces refuse a cette procedure.");
                }

                var isPilot = process.PilotUserId == userContext.UserId;
                var isActor = await _processActorRepository.HasActorAsync(process.Id, userContext.UserId);
                if (!isPilot && !isActor)
                {
                    throw new ForbiddenException("Vous n'avez pas acces a cette procedure car vous n'etes ni le pilote ni un acteur de son processus associe.");
                }
            }

            var response = await MapToProcedureResponseAsync(procedure);
            var instructions = await _instructionRepository.GetByProcedureIdAsync(id);

            return new ProcedureDetailsResponse
            {
                Procedure = response,
                Instructions = instructions.Select(MapToInstructionResponse).ToList()
            };
        }

        public async Task<List<ProcedureListItemResponse>> GetByProcessIdAsync(int processId, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
            {
                throw new NotFoundException("Processus introuvable.");
            }

            EnsureProcedureReadAccess(userContext, process.OrganizationId);

            if (userContext.Role == UserRoles.UTILISATEUR)
            {
                var isPilot = process.PilotUserId == userContext.UserId;
                var isActor = await _processActorRepository.HasActorAsync(processId, userContext.UserId);
                if (!isPilot && !isActor)
                {
                    throw new ForbiddenException("Vous n'avez pas acces aux procedures de ce processus car vous n'etes ni son pilote ni son acteur.");
                }
            }

            var items = await _procedureRepository.GetByProcessIdAsync(processId, process.OrganizationId);
            return items.Select(MapToListItemResponse).ToList();
        }

        public async Task<ProcedureResponse> CreateAsync(CreateProcedureRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            await VerifyProcedureWritePermissionAsync(request.ProcessId, userContext);

            var organizationId = ResolveOrganizationScopeForWrite(userContext);

            await ValidateProcedurePayloadAsync(
                request.ProcessId,
                request.Code,
                request.Title,
                request.Status,
                request.ResponsibleUserId,
                organizationId,
                null);

            var procedure = new Procedure
            {
                OrganizationId = organizationId,
                ProcessId = request.ProcessId,
                Code = request.Code.Trim(),
                Title = request.Title.Trim(),
                Objective = NormalizeNullable(request.Objective),
                Scope = NormalizeNullable(request.Scope),
                Description = NormalizeNullable(request.Description),
                ResponsibleUserId = request.ResponsibleUserId,
                Status = string.IsNullOrWhiteSpace(request.Status)
                    ? ProcedureConstants.StatusActif
                    : request.Status.Trim().ToUpperInvariant(),
                VersionNumber = string.IsNullOrWhiteSpace(request.VersionNumber) ? "1.0" : request.VersionNumber.Trim(),
                RevisionComment = string.IsNullOrWhiteSpace(request.RevisionComment) ? "CrÃƒÂ©ation initiale" : request.RevisionComment.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            var id = await _procedureRepository.CreateAsync(procedure);
            var created = await GetProcedureOrThrowAsync(id);
            await EnsureResponsibleIsProcessActorAsync(created.ProcessId, created.OrganizationId, created.ResponsibleUserId);

            await LogProcedureActionAsync(
                created,
                "PROCEDURE_CREATED",
                null,
                created.Code,
                $"ProcÃƒÂ©dure crÃƒÂ©ÃƒÂ©e : '{created.Title}'.",
                userContext.UserId);

            return await MapToProcedureResponseAsync(created);
        }

        public async Task<ProcedureResponse> UpdateAsync(int id, UpdateProcedureRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var procedure = await GetProcedureOrThrowAsync(id);
            EnsureProcedureWriteAccess(userContext, procedure.OrganizationId);
            await VerifyProcedureWritePermissionAsync(procedure.ProcessId, userContext);

            // Seuls ADMIN_ORG, RESPONSABLE_QUALITE et SUPER_ADMIN peuvent changer le responsable
            var canChangeResponsible = userContext.IsSuperAdmin
                || userContext.Role == UserRoles.ADMIN_ORG
                || userContext.Role == UserRoles.RESPONSABLE_QUALITE;
            var effectiveResponsibleId = canChangeResponsible ? request.ResponsibleUserId : procedure.ResponsibleUserId;

            await ValidateProcedurePayloadAsync(
                request.ProcessId,
                request.Code,
                request.Title,
                request.Status,
                effectiveResponsibleId,
                procedure.OrganizationId,
                id);

            var oldCode = procedure.Code;
            var oldTitle = procedure.Title;
            var oldStatus = procedure.Status;
            var oldVersionNumber = procedure.VersionNumber;
            var oldRevisionComment = procedure.RevisionComment;

            procedure.ProcessId = request.ProcessId;
            procedure.Code = request.Code.Trim();
            procedure.Title = request.Title.Trim();
            procedure.Objective = NormalizeNullable(request.Objective);
            procedure.Scope = NormalizeNullable(request.Scope);
            procedure.Description = NormalizeNullable(request.Description);
            procedure.ResponsibleUserId = effectiveResponsibleId;
            procedure.Status = string.IsNullOrWhiteSpace(request.Status)
                ? ProcedureConstants.StatusActif
                : request.Status.Trim().ToUpperInvariant();

            if (decimal.TryParse(oldVersionNumber, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedVer))
            {
                procedure.VersionNumber = (parsedVer + 0.1m).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                procedure.VersionNumber = string.IsNullOrWhiteSpace(request.VersionNumber) ? oldVersionNumber : request.VersionNumber.Trim();
            }

            procedure.RevisionComment = request.RevisionComment?.Trim();
            procedure.UpdatedAt = DateTime.UtcNow;

            await _procedureRepository.UpdateAsync(procedure);
            await EnsureResponsibleIsProcessActorAsync(procedure.ProcessId, procedure.OrganizationId, procedure.ResponsibleUserId);

            var changesList = new List<string>();
            if (oldCode != procedure.Code) changesList.Add($"Code : '{oldCode}' Ã¢â€ â€™ '{procedure.Code}'");
            if (oldTitle != procedure.Title) changesList.Add($"Titre : '{oldTitle}' Ã¢â€ â€™ '{procedure.Title}'");
            if (oldStatus != procedure.Status) changesList.Add($"Statut : '{oldStatus}' Ã¢â€ â€™ '{procedure.Status}'");
            if (oldVersionNumber != procedure.VersionNumber) changesList.Add($"Version : '{oldVersionNumber}' Ã¢â€ â€™ '{procedure.VersionNumber}'");
            if (oldRevisionComment != procedure.RevisionComment) changesList.Add($"Commentaire : '{oldRevisionComment}' Ã¢â€ â€™ '{procedure.RevisionComment}'");

            var detailedComment = changesList.Any()
                ? "Modifications : " + string.Join(" | ", changesList)
                : "MÃƒÂ©tadonnÃƒÂ©es de la procÃƒÂ©dure modifiÃƒÂ©es.";

            await LogProcedureActionAsync(
                procedure,
                "PROCEDURE_UPDATED",
                $"Code: {oldCode}, Titre: {oldTitle}",
                $"Code: {procedure.Code}, Titre: {procedure.Title}",
                detailedComment,
                userContext.UserId);

            return await MapToProcedureResponseAsync(procedure);
        }

        public async Task<bool> DeleteAsync(int id, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var procedure = await GetProcedureOrThrowAsync(id);
            EnsureProcedureWriteAccess(userContext, procedure.OrganizationId);
            await VerifyProcedureWritePermissionAsync(procedure.ProcessId, userContext);

            var deleted = await _procedureRepository.DeleteAsync(id);
            if (deleted)
            {
                await LogProcedureActionAsync(
                    procedure,
                    "PROCEDURE_DELETED",
                    procedure.Code,
                    null,
                    $"ProcÃƒÂ©dure '{procedure.Title}' supprimÃƒÂ©e.",
                    userContext.UserId);
            }

            return deleted;
        }

        public async Task<ProcedureResponse> ToggleStatusAsync(int id, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var procedure = await GetProcedureOrThrowAsync(id);
            EnsureProcedureWriteAccess(userContext, procedure.OrganizationId);
            await VerifyProcedureWritePermissionAsync(procedure.ProcessId, userContext);

            var nextStatus = procedure.Status == ProcedureConstants.StatusActif
                ? ProcedureConstants.StatusInactif
                : ProcedureConstants.StatusActif;

            var prevStatus = procedure.Status;
            await _procedureRepository.ToggleStatusAsync(id, nextStatus);
            procedure.Status = nextStatus;
            procedure.UpdatedAt = DateTime.UtcNow;

            await LogProcedureActionAsync(
                procedure,
                "STATUS_TOGGLED",
                prevStatus,
                nextStatus,
                $"Statut changÃƒÂ© de {prevStatus} ÃƒÂ  {nextStatus}.",
                userContext.UserId);

            return await MapToProcedureResponseAsync(procedure);
        }

        public async Task<ProcedureStatisticsResponse> GetStatisticsAsync(UserContext userContext)
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

            var procedures = (await _procedureRepository.GetByOrganizationAsync(organizationScope, restrictedUserId)).ToList();

            return new ProcedureStatisticsResponse
            {
                Total = procedures.Count,
                Active = procedures.Count(p => p.Status == ProcedureConstants.StatusActif),
                Inactive = procedures.Count(p => p.Status == ProcedureConstants.StatusInactif),
                WithResponsible = procedures.Count(p => p.ResponsibleUserId.HasValue),
                WithoutResponsible = procedures.Count(p => !p.ResponsibleUserId.HasValue),
                ByStatus = new Dictionary<string, int>
                {
                    [ProcedureConstants.StatusActif] = procedures.Count(p => p.Status == ProcedureConstants.StatusActif),
                    [ProcedureConstants.StatusInactif] = procedures.Count(p => p.Status == ProcedureConstants.StatusInactif)
                }
            };
        }

        public async Task<List<InstructionResponse>> GetInstructionsAsync(int procedureId, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var procedure = await GetProcedureOrThrowAsync(procedureId);
            EnsureProcedureReadAccess(userContext, procedure.OrganizationId);

            if (userContext.Role == UserRoles.UTILISATEUR)
            {
                var process = await _processRepository.GetByIdAsync(procedure.ProcessId);
                if (process == null)
                {
                    throw new ForbiddenException("Acces refuse a cette procedure.");
                }

                var isPilot = process.PilotUserId == userContext.UserId;
                var isActor = await _processActorRepository.HasActorAsync(process.Id, userContext.UserId);
                if (!isPilot && !isActor)
                {
                    throw new ForbiddenException("Vous n'avez pas acces a ces instructions car vous n'etes ni le pilote ni un acteur du processus associe.");
                }
            }

            var instructions = await _instructionRepository.GetByProcedureIdAsync(procedureId);
            return instructions.Select(MapToInstructionResponse).ToList();
        }

        public async Task<InstructionResponse> CreateInstructionAsync(int procedureId, CreateInstructionRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var procedure = await GetProcedureOrThrowAsync(procedureId);
            EnsureProcedureWriteAccess(userContext, procedure.OrganizationId);

            await ValidateInstructionPayloadAsync(procedureId, request.Code, request.Title, request.Status, null);

            var orderIndex = request.OrderIndex.HasValue && request.OrderIndex.Value > 0
                ? request.OrderIndex.Value
                : await _instructionRepository.GetNextOrderIndexAsync(procedureId);

            var instruction = new Instruction
            {
                OrganizationId = procedure.OrganizationId,
                ProcedureId = procedureId,
                Code = request.Code.Trim(),
                Title = request.Title.Trim(),
                Description = NormalizeNullable(request.Description),
                Status = string.IsNullOrWhiteSpace(request.Status)
                    ? ProcedureConstants.StatusActif
                    : request.Status.Trim().ToUpperInvariant(),
                OrderIndex = orderIndex,
                CreatedAt = DateTime.UtcNow
            };

            var id = await _instructionRepository.CreateAsync(instruction);
            var created = await _instructionRepository.GetByIdAsync(id);
            if (created == null)
            {
                throw new ServiceException("Instruction introuvable apres creation.");
            }

            await LogProcedureActionAsync(
                procedure,
                "INSTRUCTION_ADDED",
                null,
                created.Code,
                $"Instruction ajoutÃƒÂ©e : '{created.Title}'.",
                userContext.UserId);

            return MapToInstructionResponse(created);
        }

        public async Task<InstructionResponse> UpdateInstructionAsync(int procedureId, int instructionId, UpdateInstructionRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var procedure = await GetProcedureOrThrowAsync(procedureId);
            EnsureProcedureWriteAccess(userContext, procedure.OrganizationId);

            var instruction = await _instructionRepository.GetByIdAsync(instructionId);
            if (instruction == null)
            {
                throw new NotFoundException("Instruction introuvable.");
            }

            if (instruction.ProcedureId != procedureId)
            {
                throw new ForbiddenException("Instruction non associee a cette procedure.");
            }

            await ValidateInstructionPayloadAsync(procedureId, request.Code, request.Title, request.Status, instructionId);

            var oldCode = instruction.Code;
            var oldTitle = instruction.Title;

            instruction.Code = request.Code.Trim();
            instruction.Title = request.Title.Trim();
            instruction.Description = NormalizeNullable(request.Description);
            instruction.Status = string.IsNullOrWhiteSpace(request.Status)
                ? ProcedureConstants.StatusActif
                : request.Status.Trim().ToUpperInvariant();
            instruction.OrderIndex = request.OrderIndex.HasValue && request.OrderIndex.Value > 0
                ? request.OrderIndex.Value
                : instruction.OrderIndex;
            instruction.UpdatedAt = DateTime.UtcNow;

            await _instructionRepository.UpdateAsync(instruction);

            await LogProcedureActionAsync(
                procedure,
                "INSTRUCTION_UPDATED",
                $"Code: {oldCode}, Titre: {oldTitle}",
                $"Code: {instruction.Code}, Titre: {instruction.Title}",
                $"Instruction modifiÃƒÂ©e : '{instruction.Title}'.",
                userContext.UserId);

            return MapToInstructionResponse(instruction);
        }

        public async Task<bool> DeleteInstructionAsync(int procedureId, int instructionId, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            var procedure = await GetProcedureOrThrowAsync(procedureId);
            EnsureProcedureWriteAccess(userContext, procedure.OrganizationId);

            var instruction = await _instructionRepository.GetByIdAsync(instructionId);
            if (instruction == null)
            {
                throw new NotFoundException("Instruction introuvable.");
            }

            if (instruction.ProcedureId != procedureId)
            {
                throw new ForbiddenException("Instruction non associee a cette procedure.");
            }

            var deleted = await _instructionRepository.DeleteAsync(instructionId);
            if (deleted)
            {
                await LogProcedureActionAsync(
                    procedure,
                    "INSTRUCTION_DELETED",
                    instruction.Code,
                    null,
                    $"Instruction supprimÃƒÂ©e : '{instruction.Title}'.",
                    userContext.UserId);
            }

            return deleted;
        }

        private async Task ValidateProcedurePayloadAsync(
            int processId,
            string code,
            string title,
            string status,
            int? responsibleUserId,
            int organizationId,
            int? excludeProcedureId)
        {
            if (processId <= 0)
            {
                throw new ServiceException("Le processus est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ServiceException("Le code de la procedure est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ServiceException("Le titre de la procedure est obligatoire.");
            }

            var normalizedStatus = NormalizeUpper(status);
            if (string.IsNullOrWhiteSpace(normalizedStatus) || !ProcedureConstants.AllowedStatuses.Contains(normalizedStatus))
            {
                throw new ServiceException("Le statut de la procedure est invalide.");
            }

            var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
            {
                throw new ServiceException("Le processus selectionne est introuvable.");
            }

            if (process.OrganizationId != organizationId)
            {
                throw new ForbiddenException("Le processus doit appartenir a la meme organisation.");
            }

            var codeExists = await _procedureRepository.ExistsCodeAsync(organizationId, code.Trim(), excludeProcedureId);
            if (codeExists)
            {
                throw new ServiceException("Ce code de procedure existe deja dans l'organisation.");
            }

            await ValidateResponsibleAsync(responsibleUserId, organizationId);
        }

        private async Task ValidateInstructionPayloadAsync(
            int procedureId,
            string code,
            string title,
            string status,
            int? excludeInstructionId)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ServiceException("Le code de l'instruction est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ServiceException("Le titre de l'instruction est obligatoire.");
            }

            var normalizedStatus = NormalizeUpper(status);
            if (string.IsNullOrWhiteSpace(normalizedStatus) || !ProcedureConstants.AllowedStatuses.Contains(normalizedStatus))
            {
                throw new ServiceException("Le statut de l'instruction est invalide.");
            }

            var codeExists = await _instructionRepository.ExistsCodeAsync(procedureId, code.Trim(), excludeInstructionId);
            if (codeExists)
            {
                throw new ServiceException("Ce code d'instruction existe deja pour cette procedure.");
            }
        }

        private async Task ValidateResponsibleAsync(int? responsibleUserId, int organizationId)
        {
            if (!responsibleUserId.HasValue)
            {
                return;
            }

            var user = await _userRepository.GetByIdAsync(responsibleUserId.Value);
            if (user == null || !user.IsActive)
            {
                throw new ServiceException("Le responsable selectionne est invalide ou inactif.");
            }

            if (user.OrganizationId != organizationId)
            {
                throw new ForbiddenException("Le responsable doit appartenir a la meme organisation.");
            }

            if (user.Role != UserRoles.RESPONSABLE_QUALITE && user.Role != UserRoles.SUPER_ADMIN && user.Role != UserRoles.ADMIN_ORG)
            {
                throw new ForbiddenException("Le responsable d'une procédure doit être un Responsable Qualité ou un Administrateur.");
            }
        }

        private async Task EnsureResponsibleIsProcessActorAsync(int processId, int organizationId, int? responsibleUserId)
        {
            if (!responsibleUserId.HasValue)
            {
                return;
            }

            var isAlreadyActor = await _processActorRepository.HasActorAsync(processId, responsibleUserId.Value);
            if (isAlreadyActor)
            {
                return;
            }

            await _processActorRepository.AddActorIfMissingAsync(
                processId,
                organizationId,
                responsibleUserId.Value,
                ProcessConstants.ActorPiloteProcedure);
        }

        private async Task<Procedure> GetProcedureOrThrowAsync(int id)
        {
            var procedure = await _procedureRepository.GetByIdAsync(id);
            if (procedure == null)
            {
                throw new NotFoundException("Procedure introuvable.");
            }

            return procedure;
        }

        private static ProcedureListItemResponse MapToListItemResponse(ProcedureListItemData item)
        {
            return new ProcedureListItemResponse
            {
                Id = item.Id,
                OrganizationId = item.OrganizationId,
                ProcessId = item.ProcessId,
                ProcessCode = item.ProcessCode,
                ProcessName = item.ProcessName,
                Code = item.Code,
                Title = item.Title,
                ResponsibleUserId = item.ResponsibleUserId,
                ResponsibleFullName = item.ResponsibleFullName,
                Status = item.Status,
                VersionNumber = item.VersionNumber,
                CreatedAt = item.CreatedAt
            };
        }

        private async Task<ProcedureResponse> MapToProcedureResponseAsync(Procedure procedure)
        {
            var listItem = await _procedureRepository.GetListItemByIdAsync(procedure.Id);

            // -- Build base response --
            ProcedureResponse response;
            if (listItem != null)
            {
                response = new ProcedureResponse
                {
                    Id = procedure.Id,
                    OrganizationId = procedure.OrganizationId,
                    ProcessId = procedure.ProcessId,
                    ProcessCode = listItem.ProcessCode,
                    ProcessName = listItem.ProcessName,
                    Code = procedure.Code,
                    Title = procedure.Title,
                    Objective = procedure.Objective,
                    Scope = procedure.Scope,
                    Description = procedure.Description,
                    ResponsibleUserId = procedure.ResponsibleUserId,
                    ResponsibleFullName = listItem.ResponsibleFullName,
                    Status = procedure.Status,
                    VersionNumber = procedure.VersionNumber,
                    RevisionComment = procedure.RevisionComment,
                    CreatedAt = procedure.CreatedAt,
                    UpdatedAt = procedure.UpdatedAt
                };
            }
            else
            {
                var process = await _processRepository.GetByIdAsync(procedure.ProcessId);
                var responsible = procedure.ResponsibleUserId.HasValue
                    ? await _userRepository.GetByIdAsync(procedure.ResponsibleUserId.Value)
                    : null;

                response = new ProcedureResponse
                {
                    Id = procedure.Id,
                    OrganizationId = procedure.OrganizationId,
                    ProcessId = procedure.ProcessId,
                    ProcessCode = process?.Code,
                    ProcessName = process?.Name,
                    Code = procedure.Code,
                    Title = procedure.Title,
                    Objective = procedure.Objective,
                    Scope = procedure.Scope,
                    Description = procedure.Description,
                    ResponsibleUserId = procedure.ResponsibleUserId,
                    ResponsibleFullName = responsible == null ? null : $"{responsible.FirstName} {responsible.LastName}".Trim(),
                    Status = procedure.Status,
                    VersionNumber = procedure.VersionNumber,
                    RevisionComment = procedure.RevisionComment,
                    CreatedAt = procedure.CreatedAt,
                    UpdatedAt = procedure.UpdatedAt
                };
            }

            // -- Linked processes --
            var linkedProcesses = await _processRepository.GetByProcedureIdAsync(procedure.Id, procedure.OrganizationId);
            response.Processes = linkedProcesses.Select(p => new DocApi.DTOs.Processes.ProcessListItemResponse
            {
                Id = p.Id,
                OrganizationId = p.OrganizationId,
                Code = p.Code,
                Name = p.Name,
                Type = p.Type,
                PilotUserId = p.PilotUserId,
                Status = p.Status,
                VersionNumber = p.VersionNumber,
                CreatedAt = p.CreatedAt
            }).ToList();

            // -- Linked documents --
            var linkedDocs = await _documentRepository.SearchAsync(
                1, 1000, null, null, null, null, procedure.Id,
                null, procedure.OrganizationId, false, false, null);
            response.Documents = linkedDocs.Select(d => new DocApi.DTOs.Documents.DocumentListItemResponse
            {
                Id = d.Id,
                OrganizationId = d.OrganizationId,
                Code = d.Code,
                Title = d.Title,
                Type = d.Type,
                ProcessId = d.ProcessId,
                ProcessCode = d.ProcessCode,
                ProcessName = d.ProcessName,
                ProcedureId = d.ProcedureId,
                ProcedureCode = d.ProcedureCode,
                Status = d.Status ?? "BROUILLON",
                VersionNumber = d.VersionNumber,
                ExpiryDate = d.ExpiryDate,
                UpdatedAt = d.UpdatedAt ?? d.CreatedAt,
                OwnerUserId = d.OwnerUserId,
                OwnerFullName = d.OwnerFullName,
                FileName = d.FileName,
                IsActive = d.IsActive
            }).ToList();

            return response;
        }

        private static InstructionResponse MapToInstructionResponse(Instruction instruction)
        {
            return new InstructionResponse
            {
                Id = instruction.Id,
                ProcedureId = instruction.ProcedureId,
                OrganizationId = instruction.OrganizationId,
                Code = instruction.Code,
                Title = instruction.Title,
                Description = instruction.Description,
                Status = instruction.Status,
                OrderIndex = instruction.OrderIndex,
                CreatedAt = instruction.CreatedAt,
                UpdatedAt = instruction.UpdatedAt
            };
        }

        private static void EnsureCanRead(UserContext userContext)
        {
            if (!userContext.CanReadProcedures)
            {
                throw new ForbiddenException("Vous n'avez pas les droits de lecture sur les procedures.");
            }
        }

        private static void EnsureCanWrite(UserContext userContext)
        {
            if (!userContext.CanWriteProcedures)
            {
                throw new ForbiddenException("Vous n'avez pas les droits d'ecriture sur les procedures.");
            }
        }

        private static int ResolveOrganizationScopeForWrite(UserContext userContext)
        {
            if (userContext.IsSuperAdmin)
            {
                throw new ForbiddenException("Le role SUPER_ADMIN ne peut pas modifier les procedures.");
            }

            if (!userContext.OrganizationId.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable dans le token utilisateur.");
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

        private static void EnsureProcedureReadAccess(UserContext userContext, int organizationId)
        {
            if (userContext.IsSuperAdmin)
            {
                return;
            }

            if (!userContext.OrganizationId.HasValue || userContext.OrganizationId.Value != organizationId)
            {
                throw new ForbiddenException("Acces refuse a cette procedure.");
            }
        }

        private static void EnsureProcedureWriteAccess(UserContext userContext, int organizationId)
        {
            EnsureCanWrite(userContext);

            if (!userContext.OrganizationId.HasValue || userContext.OrganizationId.Value != organizationId)
            {
                throw new ForbiddenException("Acces refuse a cette procedure.");
            }
        }

        private async Task VerifyProcedureWritePermissionAsync(int processId, UserContext userContext)
        {
            if (userContext.IsSuperAdmin || userContext.Role == UserRoles.RESPONSABLE_QUALITE || userContext.Role == UserRoles.ADMIN_ORG)
            {
                return;
            }

            if (userContext.Role == UserRoles.UTILISATEUR)
            {
                var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
                {
                    throw new NotFoundException("Processus associe introuvable.");
                }

                if (process.PilotUserId == userContext.UserId)
                {
                    return;
                }

                var actors = await _processActorRepository.GetActorsByProcessIdAsync(processId);
                var userActor = actors.FirstOrDefault(a => a.UserId == userContext.UserId);
                if (userActor != null)
                {
                    var type = userActor.ActorType.Trim().ToUpperInvariant();
                    if (type == ProcessConstants.ActorPilote || type == ProcessConstants.ActorCopilote || type == ProcessConstants.ActorContributeur)
                    {
                        return;
                    }
                }

                throw new ForbiddenException("Vous n'avez pas les droits de modification sur les procedures de ce processus car les observateurs ne peuvent pas modifier.");
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

        public async Task<List<ProcedureActionLogResponse>> GetActionLogsAsync(int procedureId, UserContext userContext)
        {
            EnsureCanRead(userContext);

            var procedure = await GetProcedureOrThrowAsync(procedureId);
            EnsureProcedureReadAccess(userContext, procedure.OrganizationId);

            var logs = await _procedureActionLogRepository.GetByProcedureIdAsync(procedureId, procedure.OrganizationId);
            return logs.Select(MapToActionLogResponse).ToList();
        }

        public async Task<bool> DeleteActionLogAsync(int logId, UserContext userContext)
        {
            EnsureCanWrite(userContext);

            if (!userContext.OrganizationId.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable dans le token utilisateur.");
            }

            var log = await _procedureActionLogRepository.GetByIdAsync(logId, userContext.OrganizationId.Value);
            if (log == null)
            {
                throw new NotFoundException("Journal d'actions introuvable.");
            }

            var procedure = await GetProcedureOrThrowAsync(log.ProcedureId);
            EnsureProcedureWriteAccess(userContext, procedure.OrganizationId);
            await VerifyProcedureWritePermissionAsync(procedure.ProcessId, userContext);

            return await _procedureActionLogRepository.DeleteAsync(logId, userContext.OrganizationId.Value);
        }

        private async Task LogProcedureActionAsync(
            Procedure procedure,
            string actionType,
            string? oldValue,
            string? newValue,
            string? comment,
            int performedByUserId)
        {
            await _procedureActionLogRepository.CreateAsync(new ProcedureActionLog
            {
                OrganizationId = procedure.OrganizationId,
                ProcedureId = procedure.Id,
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
                    procedure.OrganizationId,
                    performedByUserId,
                    actorName,
                    "PROCEDURE",
                    actionType.Replace("PROCEDURE_", ""),
                    $"ProcÃƒÂ©dure {procedure.Code} : {actionType}",
                    comment ?? $"Action {actionType} effectuÃƒÂ©e sur la procÃƒÂ©dure '{procedure.Title}'.");
            }
            catch
            {
                // Ignored to avoid breaking primary database operations if logger fails
            }
        }

        private static ProcedureActionLogResponse MapToActionLogResponse(ProcedureActionLogData log)
        {
            return new ProcedureActionLogResponse
            {
                Id = log.Id,
                OrganizationId = log.OrganizationId,
                ProcedureId = log.ProcedureId,
                ActionType = log.ActionType,
                OldValue = log.OldValue,
                NewValue = log.NewValue,
                Comment = log.Comment,
                PerformedByUserId = log.PerformedByUserId,
                PerformedByFullName = log.PerformedByFullName,
                PerformedAt = log.PerformedAt
            };
        }

        public async Task<bool> AddProcessLinkAsync(int processId, int procedureId, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var procedure = await GetProcedureOrThrowAsync(procedureId);
            EnsureProcedureWriteAccess(userContext, procedure.OrganizationId);
            await VerifyProcedureWritePermissionAsync(processId, userContext);

            var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
            {
                throw new NotFoundException("Processus introuvable.");
            }

            var linked = await _procedureRepository.AddProcessLinkAsync(processId, procedureId);
            if (linked)
            {
                await EnsureResponsibleIsProcessActorAsync(processId, process.OrganizationId, procedure.ResponsibleUserId);

                await LogProcedureActionAsync(
                    procedure,
                    "PROCEDURE_UPDATED",
                    null,
                    $"LinkedToProcess: {process.Code}",
                    $"ProcÃƒÂ©dure liÃƒÂ©e au processus '{process.Name}'.",
                    userContext.UserId);
            }

            return linked;
        }

        public async Task<bool> RemoveProcessLinkAsync(int processId, int procedureId, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var procedure = await GetProcedureOrThrowAsync(procedureId);
            EnsureProcedureWriteAccess(userContext, procedure.OrganizationId);
            await VerifyProcedureWritePermissionAsync(processId, userContext);

            var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
            {
                throw new NotFoundException("Processus introuvable.");
            }

            var unlinked = await _procedureRepository.RemoveProcessLinkAsync(processId, procedureId);
            if (unlinked)
            {
                await LogProcedureActionAsync(
                    procedure,
                    "PROCEDURE_UPDATED",
                    $"LinkedToProcess: {process.Code}",
                    null,
                    $"ProcÃƒÂ©dure dÃƒÂ©liÃƒÂ©e du processus '{process.Name}'.",
                    userContext.UserId);
            }

            return unlinked;
        }
    }
}



