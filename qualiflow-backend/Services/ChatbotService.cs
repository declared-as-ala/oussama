using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Chat;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocApi.Services
{
    public class ChatbotService : IChatbotService
    {
        private static readonly ConcurrentDictionary<int, List<ConversationMemory>> Store = new();
        private static int _conversationIdSeed = 1000;
        private static int _messageIdSeed = 10000;
        private const string ProjectAndIsoKnowledge = """
            BASE DE CONNAISSANCES QUALIFLOW ET ISO:
            - QualiFlow est une plateforme de management qualite et GED pour piloter les documents, processus, procedures, non-conformites, actions correctives, indicateurs, notifications, utilisateurs et organisations.
            - Le backend utilise ASP.NET Core Web API (.NET 8), PostgreSQL, Dapper/Npgsql, JWT, SignalR, RabbitMQ, Swagger, SMTP, Firebase Cloud Messaging et Groq.
            - Le frontend web utilise Angular 17, Angular Material et TypeScript. Le mobile utilise Ionic/Angular et Capacitor.
            - Roles principaux: SUPER_ADMIN, ADMIN_ORG, RESPONSABLE_QUALITE, UTILISATEUR.
            - Workflow documentaire typique: creation du document, ajout d'une version, statut EN_REVISION, verification/approbation, publication, consultation, archivage ou gestion de peremption.
            - Les documents peuvent etre lies aux processus, procedures, proprietaires et versions.
            - Les non-conformites servent a enregistrer les ecarts, analyser les causes, suivre les statuts et declencher des actions correctives.
            - Les actions correctives servent a traiter les causes d'ecarts, suivre les responsables, echeances, preuves et verification d'efficacite.
            - Les indicateurs/KPI servent a mesurer la performance des processus et a appuyer l'amelioration continue.

            REPERES ISO 9001:
            - ISO 9001 est orientee systeme de management de la qualite.
            - Articles frequents: 4 Contexte de l'organisme, 5 Leadership, 6 Planification, 7 Support, 8 Realisation des activites operationnelles, 9 Evaluation des performances, 10 Amelioration.
            - Liens QualiFlow: documents et procedures pour l'information documentee, processus pour l'approche processus, indicateurs pour la performance, non-conformites/actions correctives pour l'amelioration.

            REPERES ISO 21001:
            - ISO 21001 est orientee organismes d'education/formation et systeme de management des organismes educatifs.
            - Elle insiste sur les besoins des apprenants et beneficiaires, l'accessibilite, l'equite, la conception des services educatifs, l'evaluation et l'amelioration.
            - Liens QualiFlow: procedures pedagogiques, gestion documentaire, indicateurs de satisfaction/performance, traitement des reclamations et non-conformites, actions d'amelioration.

            LIMITES:
            - L'assistant aide a comprendre, structurer et appliquer les exigences ISO dans QualiFlow.
            - Il ne remplace pas un auditeur certifie ni le texte officiel des normes ISO.
            """;

        private readonly IOpenRouterService _openRouterService;
        private readonly IDocumentRepository _documentRepository;
        private readonly IProcessRepository _processRepository;
        private readonly IProcedureRepository _procedureRepository;
        private readonly INonConformityRepository _nonConformityRepository;
        private readonly IIndicatorRepository _indicatorRepository;
        private readonly ILogger<ChatbotService> _logger;

        public ChatbotService(
            IOpenRouterService openRouterService,
            IDocumentRepository documentRepository,
            IProcessRepository processRepository,
            IProcedureRepository procedureRepository,
            INonConformityRepository nonConformityRepository,
            IIndicatorRepository indicatorRepository,
            ILogger<ChatbotService> logger)
        {
            _openRouterService = openRouterService;
            _documentRepository = documentRepository;
            _processRepository = processRepository;
            _procedureRepository = procedureRepository;
            _nonConformityRepository = nonConformityRepository;
            _indicatorRepository = indicatorRepository;
            _logger = logger;
        }

        public async Task<AskChatResponseDto> AskAsync(
            AskChatRequestDto request,
            UserContext userContext,
            CancellationToken cancellationToken = default)
        {
            var question = request.Question?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(question))
            {
                throw new ServiceException("La question est obligatoire.");
            }

            var conversation = GetOrCreateConversation(userContext, request.ConversationId, BuildConversationTitle(question));
            AddMessage(conversation, "USER", question);

            var answer = await GenerateAnswerAsync(question, userContext, cancellationToken);
            var assistantMessage = AddMessage(conversation, "ASSISTANT", answer);
            conversation.UpdatedAt = DateTime.UtcNow;

            return new AskChatResponseDto
            {
                ConversationId = conversation.Id,
                Answer = answer,
                AssistantMessage = MapMessage(assistantMessage)
            };
        }

        public Task<IReadOnlyList<ChatConversationDto>> GetConversationsAsync(
            UserContext userContext,
            CancellationToken cancellationToken = default)
        {
            var conversations = GetUserConversations(userContext.UserId)
                .OrderByDescending(c => c.UpdatedAt)
                .Select(MapConversation)
                .ToList()
                .AsReadOnly();

            return Task.FromResult((IReadOnlyList<ChatConversationDto>)conversations);
        }

        public Task<ChatConversationDetailsDto> GetConversationByIdAsync(
            int conversationId,
            UserContext userContext,
            CancellationToken cancellationToken = default)
        {
            var conversation = FindConversation(userContext.UserId, conversationId)
                ?? throw new NotFoundException("Conversation introuvable.");

            var result = new ChatConversationDetailsDto
            {
                Id = conversation.Id,
                Title = conversation.Title,
                CreatedAt = conversation.CreatedAt,
                UpdatedAt = conversation.UpdatedAt,
                Messages = conversation.Messages
                    .OrderBy(m => m.CreatedAt)
                    .Select(MapMessage)
                    .ToList()
            };

            return Task.FromResult(result);
        }

        public Task<ChatConversationDto> CreateConversationAsync(
            CreateConversationDto request,
            UserContext userContext,
            CancellationToken cancellationToken = default)
        {
            var title = string.IsNullOrWhiteSpace(request.Title)
                ? "Nouvelle conversation"
                : request.Title.Trim();

            var createdAt = DateTime.UtcNow;
            var conversation = new ConversationMemory
            {
                Id = Interlocked.Increment(ref _conversationIdSeed),
                UserId = userContext.UserId,
                Title = title,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };

            var conversations = GetUserConversations(userContext.UserId);
            lock (conversations)
            {
                conversations.Add(conversation);
            }

            return Task.FromResult(MapConversation(conversation));
        }

        public Task<bool> DeleteConversationAsync(
            int conversationId,
            UserContext userContext,
            CancellationToken cancellationToken = default)
        {
            var conversations = GetUserConversations(userContext.UserId);
            lock (conversations)
            {
                var index = conversations.FindIndex(c => c.Id == conversationId);
                if (index < 0)
                {
                    return Task.FromResult(false);
                }

                conversations.RemoveAt(index);
                return Task.FromResult(true);
            }
        }

        private async Task<string> GenerateAnswerAsync(
            string question,
            UserContext userContext,
            CancellationToken cancellationToken)
        {
            var context = await BuildContextAsync(question, userContext, cancellationToken);
            var effectiveContext = string.IsNullOrWhiteSpace(context)
                ? ProjectAndIsoKnowledge
                : ProjectAndIsoKnowledge + "\n\nDONNEES TROUVEES DANS QUALIFLOW:\n" + context;
            try
            {
                return await _openRouterService.GenerateAnswerAsync(
                    BuildSystemPrompt(),
                    question,
                    effectiveContext,
                    cancellationToken);
            }
            catch (ServiceException ex)
            {
                _logger.LogWarning(ex, "Groq indisponible. Bascule vers le mode secours local.");
                return BuildLocalFallbackAnswer(question, effectiveContext);
            }
        }

        private static string BuildLocalFallbackAnswer(string question, string context)
        {
            var effectiveContext = string.IsNullOrWhiteSpace(context)
                ? ProjectAndIsoKnowledge
                : context;

            var normalizedQuestion = question.ToLowerInvariant();
            var wantsIsoExplanation =
                normalizedQuestion.Contains("iso", StringComparison.Ordinal) ||
                normalizedQuestion.Contains("9001", StringComparison.Ordinal) ||
                normalizedQuestion.Contains("21001", StringComparison.Ordinal) ||
                normalizedQuestion.Contains("certification", StringComparison.Ordinal) ||
                normalizedQuestion.Contains("audit", StringComparison.Ordinal) ||
                normalizedQuestion.Contains("qualite", StringComparison.Ordinal) ||
                normalizedQuestion.Contains("qualité", StringComparison.Ordinal);
            var wantsDetailedExplanation =
                normalizedQuestion.Contains("explique", StringComparison.Ordinal) ||
                normalizedQuestion.Contains("detail", StringComparison.Ordinal) ||
                normalizedQuestion.Contains("etape", StringComparison.Ordinal) ||
                normalizedQuestion.Contains("processus", StringComparison.Ordinal) ||
                normalizedQuestion.Contains("procedure", StringComparison.Ordinal) ||
                wantsIsoExplanation;

            if (!wantsDetailedExplanation)
            {
                return
                    "Je reponds en mode secours local (Groq indisponible), a partir de la base QualiFlow/ISO et des donnees trouvees.\n\n" +
                    "Contexte pertinent:\n" +
                    effectiveContext;
            }

            return
                "Je reponds en mode secours local (OpenRouter indisponible), a partir de la base QualiFlow/ISO et des donnees trouvees.\n\n" +
                "1. Definition\n" +
                "- QualiFlow est une plateforme GED et management qualite qui aide a structurer les informations documentees, les processus, les procedures, les non-conformites, les actions correctives et les indicateurs.\n\n" +
                "2. Objectif\n" +
                "- Aider l'organisation a piloter sa conformite ISO 9001/21001, suivre les preuves, mesurer la performance et soutenir l'amelioration continue.\n\n" +
                "3. Acteurs impliques\n" +
                "- SUPER_ADMIN, ADMIN_ORG, RESPONSABLE_QUALITE et UTILISATEUR selon les droits et responsabilites.\n\n" +
                "4. Conditions d'entree\n" +
                "- Documents, processus, procedures, ecarts, indicateurs ou exigences ISO a analyser.\n\n" +
                "5. Etapes detaillees\n" +
                "- Analyse de la question\n" +
                "- Recherche des elements correspondants dans QualiFlow\n" +
                "- Liaison avec les clauses ISO pertinentes si la question le demande\n" +
                "- Proposition d'une reponse exploitable sans presenter comme certain ce qui n'est pas dans les donnees\n\n" +
                "6. Documents ou formulaires associes\n" +
                "- Manuel qualite, procedures, enregistrements, preuves, rapports de non-conformite, plans d'actions et tableaux d'indicateurs.\n\n" +
                "7. Regles de validation / gestion\n" +
                "- Les versions documentaires doivent etre verifiees, approuvees et publiees selon le workflow defini.\n\n" +
                "8. Resultat / sortie\n" +
                "- Reponse contextualisee avec recommandations pratiques pour QualiFlow et la demarche ISO.\n\n" +
                "9. Lien avec les autres modules de la plateforme\n" +
                "- Processus, procedures, documents, non-conformites, actions correctives, indicateurs et notifications.\n\n" +
                "Contexte pertinent:\n" +
                effectiveContext;
        }

        private async Task<string> BuildContextAsync(
            string question,
            UserContext userContext,
            CancellationToken cancellationToken)
        {
            var organizationId = userContext.OrganizationId;
            var buffer = new StringBuilder();

            var documents = await _documentRepository.SearchAsync(
                pageNumber: 1,
                pageSize: 5,
                search: question,
                type: null,
                status: null,
                processId: null,
                procedureId: null,
                ownerUserId: null,
                organizationId: organizationId,
                pendingValidationOnly: false,
                hidePendingValidationFromGlobal: false);

            var processes = await _processRepository.SearchAsync(
                pageNumber: 1,
                pageSize: 5,
                search: question,
                type: null,
                status: null,
                pilotUserId: null,
                organizationId: organizationId);

            var procedures = await _procedureRepository.SearchAsync(
                pageNumber: 1,
                pageSize: 5,
                search: question,
                processId: null,
                status: null,
                responsibleUserId: null,
                organizationId: organizationId);

            if (organizationId.HasValue)
            {
                var nonConformities = await _nonConformityRepository.SearchAsync(
                    pageNumber: 1,
                    pageSize: 5,
                    search: question,
                    status: null,
                    severity: null,
                    processId: null,
                    responsibleUserId: null,
                    organizationId: organizationId.Value);

                var indicators = await _indicatorRepository.SearchAsync(
                    pageNumber: 1,
                    pageSize: 5,
                    search: question,
                    status: null,
                    processId: null,
                    measurementFrequency: null,
                    responsibleUserId: null,
                    isInAlert: null,
                    organizationId: organizationId.Value);

                AppendNonConformities(buffer, nonConformities);
                AppendIndicators(buffer, indicators);
            }

            AppendDocuments(buffer, documents);
            AppendProcesses(buffer, processes);
            AppendProcedures(buffer, procedures);

            var context = buffer.ToString().Trim();
            return context;
        }

        private static void AppendDocuments(StringBuilder builder, IEnumerable<Domain.Entities.DocumentListItemData> documents)
        {
            var list = documents.ToList();
            if (!list.Any())
            {
                return;
            }

            builder.AppendLine("DOCUMENTS:");
            foreach (var item in list)
            {
                builder.AppendLine(
                    $"- [{item.Code}] {item.Title} | Type: {item.Type} | Statut: {item.Status ?? "N/A"} | Version: {item.VersionNumber ?? "N/A"}");
            }

            builder.AppendLine();
        }

        private static void AppendProcesses(StringBuilder builder, IEnumerable<Domain.Entities.Process> processes)
        {
            var list = processes.ToList();
            if (!list.Any())
            {
                return;
            }

            builder.AppendLine("PROCESSUS:");
            foreach (var item in list)
            {
                builder.AppendLine(
                    $"- [{item.Code}] {item.Name} | Type: {item.Type} | Statut: {item.Status} | Description: {item.Description ?? "N/A"}");
            }

            builder.AppendLine();
        }

        private static void AppendProcedures(StringBuilder builder, IEnumerable<Domain.Entities.ProcedureListItemData> procedures)
        {
            var list = procedures.ToList();
            if (!list.Any())
            {
                return;
            }

            builder.AppendLine("PROCEDURES:");
            foreach (var item in list)
            {
                builder.AppendLine(
                    $"- [{item.Code}] {item.Title} | Processus: {item.ProcessName ?? "N/A"} | Statut: {item.Status ?? "N/A"} | Responsable: {item.ResponsibleFullName ?? "N/A"}");
            }

            builder.AppendLine();
        }

        private static void AppendNonConformities(StringBuilder builder, IEnumerable<Domain.Entities.NonConformityListItemData> nonConformities)
        {
            var list = nonConformities.ToList();
            if (!list.Any()) return;

            builder.AppendLine("NON-CONFORMITES:");
            foreach (var item in list)
            {
                builder.AppendLine($"- [{item.Code}] {item.Title} | Gravite: {item.Severity} | Statut: {item.Status} | Detecte le: {item.DetectedDate:dd/MM/yyyy}");
            }
            builder.AppendLine();
        }

        private static void AppendIndicators(StringBuilder builder, IEnumerable<Domain.Entities.IndicatorListItemData> indicators)
        {
            var list = indicators.ToList();
            if (!list.Any()) return;

            builder.AppendLine("INDICATEURS:");
            foreach (var item in list)
            {
                builder.AppendLine($"- [{item.Code}] {item.Name} | Unite: {item.Unit} | Cible: {item.TargetValue} | Frequence: {item.MeasurementFrequency} | Alerte: {(item.IsInAlert ? "OUI" : "NON")}");
            }
            builder.AppendLine();
        }

        private static string BuildSystemPrompt()
        {
            return
                "Tu es l'assistant IA officiel de QualiFlow, specialise en gestion documentaire, management qualite, ISO 9001 et ISO 21001.\n" +
                "Tu aides les utilisateurs a comprendre le projet QualiFlow, ses modules, ses workflows et l'application pratique des exigences ISO.\n" +
                "Quand un contexte QualiFlow est fourni, utilise-le en priorite.\n" +
                "Regles obligatoires :\n" +
                "- Reponds de maniere utile, claire et concise.\n" +
                "- Reponds toujours en francais.\n" +
                "- Pour les questions sur ISO 9001 ou ISO 21001, explique le principe, le lien avec QualiFlow et les actions pratiques a realiser dans la plateforme.\n" +
                "- Cite des modules QualiFlow pertinents: Documents, Processus, Procedures, Non-conformites, Actions correctives, Indicateurs, Notifications.\n" +
                "- Utilise le contexte fourni quand il est pertinent, sans inventer des faits presents comme certains.\n" +
                "- Si le contexte est insuffisant, indique-le puis donne la meilleure aide possible.\n" +
                "- Ne pretend pas remplacer le texte officiel ISO ni un auditeur certifie.\n" +
                "- Quand l'utilisateur demande une explication d'un processus ou d'une procedure, reponds de maniere detaillee et structuree avec : Definition, Objectif, Acteurs impliques, Conditions d'entree, Etapes detaillees, Documents associes, Regles de validation, Resultat, Lien avec les modules.";
        }

        private static List<ConversationMemory> GetUserConversations(int userId)
        {
            return Store.GetOrAdd(userId, _ => new List<ConversationMemory>());
        }

        private static ConversationMemory? FindConversation(int userId, int conversationId)
        {
            var conversations = GetUserConversations(userId);
            lock (conversations)
            {
                return conversations.FirstOrDefault(c => c.Id == conversationId);
            }
        }

        private static ConversationMemory GetOrCreateConversation(UserContext userContext, int? conversationId, string defaultTitle)
        {
            if (conversationId.HasValue)
            {
                var existing = FindConversation(userContext.UserId, conversationId.Value);
                if (existing != null)
                {
                    return existing;
                }
            }

            var createdAt = DateTime.UtcNow;
            var created = new ConversationMemory
            {
                Id = Interlocked.Increment(ref _conversationIdSeed),
                UserId = userContext.UserId,
                Title = defaultTitle,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };

            var conversations = GetUserConversations(userContext.UserId);
            lock (conversations)
            {
                conversations.Add(created);
            }

            return created;
        }

        private static ChatMessageMemory AddMessage(ConversationMemory conversation, string role, string content)
        {
            var message = new ChatMessageMemory
            {
                Id = Interlocked.Increment(ref _messageIdSeed),
                ConversationId = conversation.Id,
                Role = role,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            lock (conversation.Messages)
            {
                conversation.Messages.Add(message);
            }

            return message;
        }

        private static string BuildConversationTitle(string question)
        {
            var clean = question.Trim();
            if (clean.Length <= 70)
            {
                return clean;
            }

            return $"{clean[..67]}...";
        }

        private static ChatConversationDto MapConversation(ConversationMemory memory)
        {
            return new ChatConversationDto
            {
                Id = memory.Id,
                Title = memory.Title,
                CreatedAt = memory.CreatedAt,
                UpdatedAt = memory.UpdatedAt
            };
        }

        private static ChatMessageDto MapMessage(ChatMessageMemory memory)
        {
            return new ChatMessageDto
            {
                Id = memory.Id,
                ConversationId = memory.ConversationId,
                Role = memory.Role,
                Content = memory.Content,
                CreatedAt = memory.CreatedAt
            };
        }

        private sealed class ConversationMemory
        {
            public int Id { get; init; }
            public int UserId { get; init; }
            public string Title { get; set; } = string.Empty;
            public DateTime CreatedAt { get; init; }
            public DateTime UpdatedAt { get; set; }
            public List<ChatMessageMemory> Messages { get; } = new();
        }

        private sealed class ChatMessageMemory
        {
            public int Id { get; init; }
            public int ConversationId { get; init; }
            public string Role { get; init; } = "ASSISTANT";
            public string Content { get; init; } = string.Empty;
            public DateTime CreatedAt { get; init; }
        }
    }
}
