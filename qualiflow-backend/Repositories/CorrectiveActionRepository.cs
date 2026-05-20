using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class CorrectiveActionRepository : ICorrectiveActionRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public CorrectiveActionRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> SyncOverdueStatusesAsync(int? organizationId = null, int? nonConformityId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE CorrectiveActions ca
                SET
                    Status = CASE
                        WHEN ca.Status = @LegacyTodo THEN @Planned
                        WHEN ca.Status = @LegacyDone THEN @Completed
                        WHEN ca.Status = @LegacyOverdue THEN @InProgress
                        ELSE ca.Status
                    END,
                    UpdatedAt = CASE
                        WHEN ca.Status IN (@LegacyTodo, @LegacyDone, @LegacyOverdue) THEN NOW()
                        ELSE ca.UpdatedAt
                    END
                FROM NonConformities nc
                WHERE ca.NonConformityId = nc.Id
                  AND ca.Status IN (@LegacyTodo, @LegacyDone, @LegacyOverdue)
                  AND (@OrganizationId IS NULL OR ca.OrganizationId = @OrganizationId)
                  AND (@NonConformityId IS NULL OR ca.NonConformityId = @NonConformityId)";

            return await connection.ExecuteAsync(sql, new
            {
                LegacyTodo = CorrectiveActionConstants.LegacyStatusTodo,
                LegacyDone = CorrectiveActionConstants.LegacyStatusDone,
                LegacyOverdue = CorrectiveActionConstants.LegacyStatusOverdue,
                Planned = CorrectiveActionConstants.StatusPlanned,
                InProgress = CorrectiveActionConstants.StatusInProgress,
                Completed = CorrectiveActionConstants.StatusCompleted,
                OrganizationId = organizationId,
                NonConformityId = nonConformityId
            });
        }

        public async Task<IEnumerable<CorrectiveActionData>> GetByNonConformityIdAsync(int nonConformityId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    ca.Id,
                    ca.OrganizationId,
                    ca.NonConformityId,
                    nc.Code AS NonConformityCode,
                    COALESCE(NULLIF(ca.Type, ''), @DefaultType) AS Type,
                    ca.Title,
                    ca.Description,
                    ca.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    ca.DueDate,
                    CASE
                        WHEN ca.Status = @LegacyTodo THEN @Planned
                        WHEN ca.Status = @LegacyDone THEN @Completed
                        WHEN ca.Status = @LegacyOverdue THEN @InProgress
                        ELSE ca.Status
                    END AS Status,
                    ca.CompletionDate,
                    ca.EffectivenessVerified,
                    ca.EffectivenessComment,
                    ca.ProofRecordId,
                    ca.CreatedAt,
                    ca.UpdatedAt
                FROM CorrectiveActions ca
                INNER JOIN NonConformities nc ON nc.Id = ca.NonConformityId
                LEFT JOIN Users u ON u.Id = ca.ResponsibleUserId
                WHERE ca.NonConformityId = @NonConformityId
                ORDER BY ca.DueDate ASC, ca.Id ASC";

            return await connection.QueryAsync<CorrectiveActionData>(sql, new
            {
                NonConformityId = nonConformityId,
                DefaultType = CorrectiveActionConstants.TypeCorrective,
                LegacyTodo = CorrectiveActionConstants.LegacyStatusTodo,
                LegacyDone = CorrectiveActionConstants.LegacyStatusDone,
                LegacyOverdue = CorrectiveActionConstants.LegacyStatusOverdue,
                Planned = CorrectiveActionConstants.StatusPlanned,
                InProgress = CorrectiveActionConstants.StatusInProgress,
                Completed = CorrectiveActionConstants.StatusCompleted
            });
        }

        public async Task<IEnumerable<CorrectiveActionListItemData>> SearchAsync(
            int pageNumber,
            int pageSize,
            string? search,
            string? status,
            string? type,
            int? responsibleUserId,
            int? nonConformityId,
            bool? isOverdue,
            DateTime? fromDate,
            DateTime? toDate,
            int organizationId,
            int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(
                parameters,
                search,
                status,
                type,
                responsibleUserId,
                nonConformityId,
                isOverdue,
                fromDate,
                toDate,
                organizationId,
                alias: "ca",
                restrictedUserId: restrictedUserId);

            parameters.Add("@PageSize", pageSize);
            parameters.Add("@Offset", (pageNumber - 1) * pageSize);

            var sql = $@"
                SELECT
                    ca.Id,
                    ca.OrganizationId,
                    ca.NonConformityId,
                    nc.Code AS NonConformityCode,
                    COALESCE(NULLIF(ca.Type, ''), @DefaultType) AS Type,
                    ca.Title,
                    ca.Description,
                    ca.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    ca.DueDate,
                    CASE
                        WHEN ca.Status = @LegacyTodo THEN @Planned
                        WHEN ca.Status = @LegacyDone THEN @Completed
                        WHEN ca.Status = @LegacyOverdue THEN @InProgress
                        ELSE ca.Status
                    END AS Status,
                    ca.CompletionDate,
                    ca.EffectivenessVerified,
                    ca.ProofRecordId,
                    ca.CreatedAt,
                    ca.UpdatedAt
                FROM CorrectiveActions ca
                INNER JOIN NonConformities nc ON nc.Id = ca.NonConformityId
                LEFT JOIN Users u ON u.Id = ca.ResponsibleUserId
                {whereClause}
                ORDER BY ca.DueDate ASC, ca.CreatedAt DESC, ca.Id DESC
                LIMIT @PageSize OFFSET @Offset";

            return await connection.QueryAsync<CorrectiveActionListItemData>(sql, parameters);
        }

        public async Task<int> CountSearchAsync(
            string? search,
            string? status,
            string? type,
            int? responsibleUserId,
            int? nonConformityId,
            bool? isOverdue,
            DateTime? fromDate,
            DateTime? toDate,
            int organizationId,
            int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(
                parameters,
                search,
                status,
                type,
                responsibleUserId,
                nonConformityId,
                isOverdue,
                fromDate,
                toDate,
                organizationId,
                alias: "ca",
                restrictedUserId: restrictedUserId);

            var sql = $@"
                SELECT COUNT(1)
                FROM CorrectiveActions ca
                INNER JOIN NonConformities nc ON nc.Id = ca.NonConformityId
                LEFT JOIN Users u ON u.Id = ca.ResponsibleUserId
                {whereClause}";

            return await connection.QuerySingleAsync<int>(sql, parameters);
        }

        public async Task<CorrectiveActionDetailsData?> GetDetailsByIdAsync(int id, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    ca.Id,
                    ca.OrganizationId,
                    ca.NonConformityId,
                    nc.Code AS NonConformityCode,
                    nc.Title AS NonConformityTitle,
                    nc.Description AS NonConformityDescription,
                    COALESCE(NULLIF(ca.Type, ''), @DefaultType) AS Type,
                    ca.Title,
                    ca.Description,
                    ca.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    u.Email AS ResponsibleEmail,
                    ca.DueDate,
                    CASE
                        WHEN ca.Status = @LegacyTodo THEN @Planned
                        WHEN ca.Status = @LegacyDone THEN @Completed
                        WHEN ca.Status = @LegacyOverdue THEN @InProgress
                        ELSE ca.Status
                    END AS Status,
                    ca.CompletionDate,
                    ca.EffectivenessVerified,
                    ca.EffectivenessComment,
                    ca.ProofRecordId,
                    pr.Code AS ProofRecordCode,
                    pr.Title AS ProofRecordTitle,
                    pr.Type AS ProofRecordType,
                    ca.CreatedAt,
                    ca.UpdatedAt
                FROM CorrectiveActions ca
                INNER JOIN NonConformities nc ON nc.Id = ca.NonConformityId
                LEFT JOIN Users u ON u.Id = ca.ResponsibleUserId
                LEFT JOIN Documents pr ON pr.Id = ca.ProofRecordId
                WHERE ca.Id = @Id
                  AND ca.OrganizationId = @OrganizationId";

            return await connection.QueryFirstOrDefaultAsync<CorrectiveActionDetailsData>(sql, new
            {
                Id = id,
                OrganizationId = organizationId,
                DefaultType = CorrectiveActionConstants.TypeCorrective,
                LegacyTodo = CorrectiveActionConstants.LegacyStatusTodo,
                LegacyDone = CorrectiveActionConstants.LegacyStatusDone,
                LegacyOverdue = CorrectiveActionConstants.LegacyStatusOverdue,
                Planned = CorrectiveActionConstants.StatusPlanned,
                InProgress = CorrectiveActionConstants.StatusInProgress,
                Completed = CorrectiveActionConstants.StatusCompleted
            });
        }

        public async Task<IEnumerable<CorrectiveActionListItemData>> GetByNonConformityForListAsync(int nonConformityId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    ca.Id,
                    ca.OrganizationId,
                    ca.NonConformityId,
                    nc.Code AS NonConformityCode,
                    COALESCE(NULLIF(ca.Type, ''), @DefaultType) AS Type,
                    ca.Title,
                    ca.Description,
                    ca.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    ca.DueDate,
                    CASE
                        WHEN ca.Status = @LegacyTodo THEN @Planned
                        WHEN ca.Status = @LegacyDone THEN @Completed
                        WHEN ca.Status = @LegacyOverdue THEN @InProgress
                        ELSE ca.Status
                    END AS Status,
                    ca.CompletionDate,
                    ca.EffectivenessVerified,
                    ca.ProofRecordId,
                    ca.CreatedAt,
                    ca.UpdatedAt
                FROM CorrectiveActions ca
                INNER JOIN NonConformities nc ON nc.Id = ca.NonConformityId
                LEFT JOIN Users u ON u.Id = ca.ResponsibleUserId
                WHERE ca.OrganizationId = @OrganizationId
                  AND ca.NonConformityId = @NonConformityId
                ORDER BY ca.DueDate ASC, ca.Id DESC";

            return await connection.QueryAsync<CorrectiveActionListItemData>(sql, new
            {
                OrganizationId = organizationId,
                NonConformityId = nonConformityId,
                DefaultType = CorrectiveActionConstants.TypeCorrective,
                LegacyTodo = CorrectiveActionConstants.LegacyStatusTodo,
                LegacyDone = CorrectiveActionConstants.LegacyStatusDone,
                LegacyOverdue = CorrectiveActionConstants.LegacyStatusOverdue,
                Planned = CorrectiveActionConstants.StatusPlanned,
                InProgress = CorrectiveActionConstants.StatusInProgress,
                Completed = CorrectiveActionConstants.StatusCompleted
            });
        }

        public async Task<IEnumerable<CorrectiveActionListItemData>> GetForStatisticsAsync(int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    ca.Id,
                    ca.OrganizationId,
                    ca.NonConformityId,
                    nc.Code AS NonConformityCode,
                    COALESCE(NULLIF(ca.Type, ''), @DefaultType) AS Type,
                    ca.Title,
                    ca.Description,
                    ca.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    ca.DueDate,
                    CASE
                        WHEN ca.Status = @LegacyTodo THEN @Planned
                        WHEN ca.Status = @LegacyDone THEN @Completed
                        WHEN ca.Status = @LegacyOverdue THEN @InProgress
                        ELSE ca.Status
                    END AS Status,
                    ca.CompletionDate,
                    ca.EffectivenessVerified,
                    ca.ProofRecordId,
                    ca.CreatedAt,
                    ca.UpdatedAt
                FROM CorrectiveActions ca
                INNER JOIN NonConformities nc ON nc.Id = ca.NonConformityId
                LEFT JOIN Users u ON u.Id = ca.ResponsibleUserId
                WHERE ca.OrganizationId = @OrganizationId";

            return await connection.QueryAsync<CorrectiveActionListItemData>(sql, new
            {
                OrganizationId = organizationId,
                DefaultType = CorrectiveActionConstants.TypeCorrective,
                LegacyTodo = CorrectiveActionConstants.LegacyStatusTodo,
                LegacyDone = CorrectiveActionConstants.LegacyStatusDone,
                LegacyOverdue = CorrectiveActionConstants.LegacyStatusOverdue,
                Planned = CorrectiveActionConstants.StatusPlanned,
                InProgress = CorrectiveActionConstants.StatusInProgress,
                Completed = CorrectiveActionConstants.StatusCompleted
            });
        }

        public async Task<CorrectiveAction?> GetByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT
                    Id,
                    OrganizationId,
                    NonConformityId,
                    COALESCE(NULLIF(Type, ''), @DefaultType) AS Type,
                    Title,
                    Description,
                    ResponsibleUserId,
                    DueDate,
                    CASE
                        WHEN Status = @LegacyTodo THEN @Planned
                        WHEN Status = @LegacyDone THEN @Completed
                        WHEN Status = @LegacyOverdue THEN @InProgress
                        ELSE Status
                    END AS Status,
                    CompletionDate,
                    EffectivenessVerified,
                    EffectivenessComment,
                    ProofRecordId,
                    CreatedAt,
                    UpdatedAt
                FROM CorrectiveActions
                WHERE Id = @Id";

            return await connection.QueryFirstOrDefaultAsync<CorrectiveAction>(sql, new
            {
                Id = id,
                DefaultType = CorrectiveActionConstants.TypeCorrective,
                LegacyTodo = CorrectiveActionConstants.LegacyStatusTodo,
                LegacyDone = CorrectiveActionConstants.LegacyStatusDone,
                LegacyOverdue = CorrectiveActionConstants.LegacyStatusOverdue,
                Planned = CorrectiveActionConstants.StatusPlanned,
                InProgress = CorrectiveActionConstants.StatusInProgress,
                Completed = CorrectiveActionConstants.StatusCompleted
            });
        }

        public async Task<int> CreateAsync(CorrectiveAction action)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO CorrectiveActions
                    (OrganizationId, NonConformityId, Type, Title, Description, ResponsibleUserId, DueDate, Status, CompletionDate, EffectivenessVerified, EffectivenessComment, ProofRecordId, CreatedAt, UpdatedAt)
                VALUES
                    (@OrganizationId, @NonConformityId, @Type, @Title, @Description, @ResponsibleUserId, @DueDate, @Status, @CompletionDate, @EffectivenessVerified, @EffectivenessComment, @ProofRecordId, @CreatedAt, @UpdatedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, action);
        }

        public async Task<bool> UpdateAsync(CorrectiveAction action)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE CorrectiveActions
                SET NonConformityId = @NonConformityId,
                    Type = @Type,
                    Title = @Title,
                    Description = @Description,
                    ResponsibleUserId = @ResponsibleUserId,
                    DueDate = @DueDate,
                    Status = @Status,
                    CompletionDate = @CompletionDate,
                    EffectivenessVerified = @EffectivenessVerified,
                    EffectivenessComment = @EffectivenessComment,
                    ProofRecordId = @ProofRecordId,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id
                  AND OrganizationId = @OrganizationId";

            var rows = await connection.ExecuteAsync(sql, action);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int id, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                DELETE FROM CorrectiveActions
                WHERE Id = @Id
                  AND OrganizationId = @OrganizationId";

            var rows = await connection.ExecuteAsync(sql, new { Id = id, OrganizationId = organizationId });
            return rows > 0;
        }

        public async Task<bool> UpdateStatusAsync(int id, int organizationId, string status, DateTime? completionDate, DateTime updatedAt)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE CorrectiveActions
                SET Status = @Status,
                    CompletionDate = @CompletionDate,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id
                  AND OrganizationId = @OrganizationId";

            var rows = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                OrganizationId = organizationId,
                Status = status,
                CompletionDate = completionDate,
                UpdatedAt = updatedAt
            });

            return rows > 0;
        }

        public async Task<bool> UpdateEffectivenessAsync(int id, int organizationId, bool effectivenessVerified, string? effectivenessComment, DateTime updatedAt, string? status = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE CorrectiveActions
                SET EffectivenessVerified = @EffectivenessVerified,
                    EffectivenessComment = @EffectivenessComment,
                    Status = COALESCE(@Status, Status),
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id
                  AND OrganizationId = @OrganizationId";

            var rows = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                OrganizationId = organizationId,
                EffectivenessVerified = effectivenessVerified,
                EffectivenessComment = effectivenessComment,
                UpdatedAt = updatedAt,
                Status = status
            });

            return rows > 0;
        }

        public async Task<int> CountOverdueAsync(int organizationId, int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            parameters.Add("@OrganizationId", organizationId);
            parameters.Add("@LegacyTodo", CorrectiveActionConstants.LegacyStatusTodo);
            parameters.Add("@LegacyDone", CorrectiveActionConstants.LegacyStatusDone);
            parameters.Add("@LegacyOverdue", CorrectiveActionConstants.LegacyStatusOverdue);
            parameters.Add("@Planned", CorrectiveActionConstants.StatusPlanned);
            parameters.Add("@InProgress", CorrectiveActionConstants.StatusInProgress);
            parameters.Add("@Completed", CorrectiveActionConstants.StatusCompleted);
            parameters.Add("@Verified", CorrectiveActionConstants.StatusVerified);

            var restrictedFilter = "";
            if (restrictedUserId.HasValue)
            {
                restrictedFilter = @"AND ca.NonConformityId IN (
                    SELECT Id FROM NonConformities WHERE ProcessId IN (
                        SELECT Id FROM Processes WHERE PilotUserId = @RestrictedUserId
                        UNION
                        SELECT ProcessId FROM ProcessActors WHERE UserId = @RestrictedUserId
                    )
                )";
                parameters.Add("@RestrictedUserId", restrictedUserId.Value);
            }

            var sql = $@"
                SELECT COUNT(1)
                FROM CorrectiveActions ca
                WHERE ca.OrganizationId = @OrganizationId
                  AND ca.DueDate::date < CURRENT_DATE
                  AND (
                      CASE
                          WHEN ca.Status = @LegacyDone THEN @Completed
                          WHEN ca.Status = @LegacyTodo THEN @Planned
                          WHEN ca.Status = @LegacyOverdue THEN @InProgress
                          ELSE ca.Status
                      END
                  ) NOT IN (@Completed, @Verified)
                  {restrictedFilter}";

            return await connection.QuerySingleAsync<int>(sql, parameters);
        }

        private static string BuildWhereClause(
            DynamicParameters parameters,
            string? search,
            string? status,
            string? type,
            int? responsibleUserId,
            int? nonConformityId,
            bool? isOverdue,
            DateTime? fromDate,
            DateTime? toDate,
            int organizationId,
            string alias,
            int? restrictedUserId = null)
        {
            var conditions = new List<string>
            {
                $"{alias}.OrganizationId = @OrganizationId"
            };

            parameters.Add("@OrganizationId", organizationId);
            parameters.Add("@DefaultType", CorrectiveActionConstants.TypeCorrective);
            parameters.Add("@LegacyTodo", CorrectiveActionConstants.LegacyStatusTodo);
            parameters.Add("@LegacyDone", CorrectiveActionConstants.LegacyStatusDone);
            parameters.Add("@LegacyOverdue", CorrectiveActionConstants.LegacyStatusOverdue);
            parameters.Add("@Planned", CorrectiveActionConstants.StatusPlanned);
            parameters.Add("@InProgress", CorrectiveActionConstants.StatusInProgress);
            parameters.Add("@Completed", CorrectiveActionConstants.StatusCompleted);
            parameters.Add("@Verified", CorrectiveActionConstants.StatusVerified);

            if (!string.IsNullOrWhiteSpace(search))
            {
                conditions.Add(@"(
                    ca.Title ILIKE @Search
                    OR COALESCE(ca.Description, '') ILIKE @Search
                    OR COALESCE(nc.Code, '') ILIKE @Search
                    OR COALESCE(u.FirstName, '') ILIKE @Search
                    OR COALESCE(u.LastName, '') ILIKE @Search
                )");
                parameters.Add("@Search", $"%{search.Trim()}%");
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                conditions.Add(@"(
                    CASE
                        WHEN ca.Status = @LegacyTodo THEN @Planned
                        WHEN ca.Status = @LegacyDone THEN @Completed
                        WHEN ca.Status = @LegacyOverdue THEN @InProgress
                        ELSE ca.Status
                    END
                ) = @Status");
                parameters.Add("@Status", status.Trim());
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                conditions.Add("COALESCE(NULLIF(ca.Type, ''), @DefaultType) = @Type");
                parameters.Add("@Type", type.Trim());
            }

            if (responsibleUserId.HasValue)
            {
                conditions.Add("ca.ResponsibleUserId = @ResponsibleUserId");
                parameters.Add("@ResponsibleUserId", responsibleUserId.Value);
            }

            if (nonConformityId.HasValue)
            {
                conditions.Add("ca.NonConformityId = @NonConformityId");
                parameters.Add("@NonConformityId", nonConformityId.Value);
            }

            if (isOverdue.HasValue)
            {
                if (isOverdue.Value)
                {
                    conditions.Add(@"
                        ca.DueDate::date < CURRENT_DATE
                        AND (
                            CASE
                                WHEN ca.Status = @LegacyTodo THEN @Planned
                                WHEN ca.Status = @LegacyDone THEN @Completed
                                WHEN ca.Status = @LegacyOverdue THEN @InProgress
                                ELSE ca.Status
                            END
                        ) NOT IN (@Completed, @Verified)");
                }
                else
                {
                    conditions.Add(@"
                        (
                            ca.DueDate::date >= CURRENT_DATE
                            OR (
                                CASE
                                    WHEN ca.Status = @LegacyTodo THEN @Planned
                                    WHEN ca.Status = @LegacyDone THEN @Completed
                                    WHEN ca.Status = @LegacyOverdue THEN @InProgress
                                    ELSE ca.Status
                                END
                            ) IN (@Completed, @Verified)
                        )");
                }
            }

            if (fromDate.HasValue)
            {
                conditions.Add("ca.DueDate::date >= @FromDate");
                parameters.Add("@FromDate", fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                conditions.Add("ca.DueDate::date <= @ToDate");
                parameters.Add("@ToDate", toDate.Value.Date);
            }

            if (restrictedUserId.HasValue)
            {
                conditions.Add(@"ca.NonConformityId IN (
                    SELECT Id FROM NonConformities WHERE ProcessId IN (
                        SELECT Id FROM Processes WHERE PilotUserId = @RestrictedUserId
                        UNION
                        SELECT ProcessId FROM ProcessActors WHERE UserId = @RestrictedUserId
                    )
                )");
                parameters.Add("@RestrictedUserId", restrictedUserId.Value);
            }

            return $"WHERE {string.Join(" AND ", conditions.Where(c => !string.IsNullOrWhiteSpace(c)))}";
        }
    }
}
