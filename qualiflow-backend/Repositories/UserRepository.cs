using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;
using Npgsql;

namespace DocApi.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Users WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Users WHERE Email = @Email ORDER BY Id DESC LIMIT 1";
            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
        }

        public async Task<IReadOnlyList<User>> GetByEmailAccountsAsync(string email)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Users WHERE Email = @Email ORDER BY Id DESC";
            var rows = await connection.QueryAsync<User>(sql, new { Email = email });
            return rows.ToList();
        }

        public async Task<User?> GetByEmailAndOrganizationAsync(string email, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Users WHERE Email = @Email AND OrganizationId = @OrganizationId";
            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Email = email, OrganizationId = organizationId });
        }

        public async Task<User?> GetByPhoneAsync(string phone)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Users WHERE Phone = @Phone ORDER BY Id DESC LIMIT 1";
            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Phone = phone });
        }

        public async Task<IReadOnlyList<User>> GetByPhoneAccountsAsync(string phone)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Users WHERE Phone = @Phone ORDER BY Id DESC";
            var rows = await connection.QueryAsync<User>(sql, new { Phone = phone });
            return rows.ToList();
        }

        public async Task<User?> GetByVerificationTokenAsync(string token)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Users WHERE EmailVerificationToken = @Token";
            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Token = token });
        }

        public async Task<User?> GetByIdWithOrganizationAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT u.*, o.Name as OrganizationName 
                FROM Users u 
                LEFT JOIN Organizations o ON u.OrganizationId = o.Id 
                WHERE u.Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { Id = id });
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Users WHERE IsActive = TRUE ORDER BY CreatedAt DESC";
            return await connection.QueryAsync<User>(sql);
        }

        public async Task<IEnumerable<User>> GetByOrganizationIdAsync(int organizationId, int page = 1, int pageSize = 10)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT * FROM Users 
                WHERE OrganizationId = @OrganizationId
                ORDER BY CreatedAt DESC 
                LIMIT @PageSize OFFSET @Offset";
            
            var offset = (page - 1) * pageSize;
            return await connection.QueryAsync<User>(sql, new { OrganizationId = organizationId, Offset = offset, PageSize = pageSize });
        }

        public async Task<IEnumerable<User>> GetByIdsAsync(int organizationId, IEnumerable<int> ids)
        {
            var idArray = ids?.Distinct().ToArray() ?? Array.Empty<int>();
            if (idArray.Length == 0)
            {
                return Array.Empty<User>();
            }

            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT *
                FROM Users
                WHERE OrganizationId = @OrganizationId
                  AND Id = ANY(@Ids)
                ORDER BY Id ASC";

            return await connection.QueryAsync<User>(sql, new
            {
                OrganizationId = organizationId,
                Ids = idArray
            });
        }

        public async Task<IEnumerable<User>> GetActiveByOrganizationAndRolesAsync(int organizationId, IEnumerable<string> roles)
        {
            var roleArray = roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim().ToUpperInvariant())
                .Distinct()
                .ToArray();

            if (roleArray.Length == 0)
            {
                return Array.Empty<User>();
            }

            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT *
                FROM Users
                WHERE OrganizationId = @OrganizationId
                  AND IsActive = TRUE
                  AND Role = ANY(@Roles)
                ORDER BY CreatedAt DESC, Id DESC";

            return await connection.QueryAsync<User>(sql, new
            {
                OrganizationId = organizationId,
                Roles = roleArray
            });
        }

        public async Task<IEnumerable<User>> SearchAsync(string? searchTerm, int? organizationId, int page = 1, int pageSize = 10)
        {
            using var connection = _connectionFactory.CreateConnection();
            
            var whereClause = "WHERE 1 = 1";
            var parameters = new DynamicParameters();
            parameters.Add("@Offset", (page - 1) * pageSize);
            parameters.Add("@PageSize", pageSize);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                whereClause += " AND (FirstName ILIKE @SearchTerm OR LastName ILIKE @SearchTerm OR Email ILIKE @SearchTerm)";
                parameters.Add("@SearchTerm", $"%{searchTerm}%");
            }

            if (organizationId.HasValue)
            {
                whereClause += " AND OrganizationId = @OrganizationId";
                parameters.Add("@OrganizationId", organizationId.Value);
            }

            var sql = $@"
                SELECT * FROM Users 
                {whereClause}
                ORDER BY CreatedAt DESC 
                LIMIT @PageSize OFFSET @Offset";
            
            return await connection.QueryAsync<User>(sql, parameters);
        }

        public async Task<int> GetSearchCountAsync(string? searchTerm, int? organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            var whereClause = "WHERE 1 = 1";
            var parameters = new DynamicParameters();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                whereClause += " AND (FirstName ILIKE @SearchTerm OR LastName ILIKE @SearchTerm OR Email ILIKE @SearchTerm)";
                parameters.Add("@SearchTerm", $"%{searchTerm}%");
            }

            if (organizationId.HasValue)
            {
                whereClause += " AND OrganizationId = @OrganizationId";
                parameters.Add("@OrganizationId", organizationId.Value);
            }

            var sql = $@"
                SELECT COUNT(1)
                FROM Users
                {whereClause}";

            return await connection.QuerySingleAsync<int>(sql, parameters);
        }

        public async Task<int> GetTotalCountAsync()
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT COUNT(1) FROM Users WHERE IsActive = TRUE";
            return await connection.QuerySingleAsync<int>(sql);
        }

        public async Task<int> GetCountByOrganizationAsync(int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT COUNT(1) FROM Users WHERE OrganizationId = @OrganizationId";
            return await connection.QuerySingleAsync<int>(sql, new { OrganizationId = organizationId });
        }

        public async Task<int> CreateAsync(User user)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO Users (OrganizationId, FirstName, LastName, Email, Username, PasswordHash, Role, Function, BirthDate, PreferredLanguage, ProfilePhotoPath, IsActive, IsEmailVerified, EmailVerificationToken, EmailVerificationExpiresAt, CreatedAt) 
                VALUES (@OrganizationId, @FirstName, @LastName, @Email, @Username, @PasswordHash, @Role, @Function, @BirthDate, @PreferredLanguage, @ProfilePhotoPath, @IsActive, @IsEmailVerified, @EmailVerificationToken, @EmailVerificationExpiresAt, @CreatedAt)
                RETURNING Id;";

            try
            {
                return await connection.QuerySingleAsync<int>(sql, user);
            }
            catch (PostgresException ex) when (
                ex.SqlState == PostgresErrorCodes.UniqueViolation &&
                (
                    string.Equals(ex.ConstraintName, "idx_users_email_org_unique", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ex.ConstraintName, "idx_users_email_superadmin_unique", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ex.ConstraintName, "idx_users_email_unique", StringComparison.OrdinalIgnoreCase)
                ))
            {
                throw new ServiceException("Email already exists");
            }
        }

        public async Task<bool> UpdateAsync(User user)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Users 
                SET FirstName = @FirstName,
                    LastName = @LastName,
                    Email = @Email,
                    Username = @Email,
                    Role = @Role,
                    Function = @Function,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";
            
            var rowsAffected = await connection.ExecuteAsync(sql, user);
            return rowsAffected > 0;
        }

        public async Task<bool> UpdateProfileAsync(int id, string firstName, string lastName, DateTime? birthDate, string? phone, string? city, string preferredLanguage, DateTime updatedAt)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Users
                SET FirstName = @FirstName,
                    LastName = @LastName,
                    BirthDate = @BirthDate,
                    Phone = @Phone,
                    City = @City,
                    PreferredLanguage = @PreferredLanguage,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                FirstName = firstName,
                LastName = lastName,
                BirthDate = birthDate,
                Phone = phone,
                City = city,
                PreferredLanguage = preferredLanguage,
                UpdatedAt = updatedAt
            });

            return rowsAffected > 0;
        }

        public async Task<bool> UpdateProfilePhotoPathAsync(int id, string? profilePhotoPath, DateTime updatedAt)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Users
                SET ProfilePhotoPath = @ProfilePhotoPath,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                ProfilePhotoPath = profilePhotoPath,
                UpdatedAt = updatedAt
            });

            return rowsAffected > 0;
        }

        public async Task<bool> ToggleStatusAsync(int id, bool isActive)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE Users SET IsActive = @IsActive, UpdatedAt = NOW() WHERE Id = @Id";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, IsActive = isActive });
            return rowsAffected > 0;
        }

        public async Task<bool> UpdatePasswordAsync(int id, string passwordHash)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE Users SET PasswordHash = @PasswordHash, UpdatedAt = NOW() WHERE Id = @Id";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, PasswordHash = passwordHash });
            return rowsAffected > 0;
        }

        public async Task<bool> UpdateLastLoginAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE Users SET LastLoginAt = NOW() WHERE Id = @Id";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE Users SET IsActive = FALSE, UpdatedAt = NOW() WHERE Id = @Id";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<bool> HardDeleteAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM Users WHERE Id = @Id";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<bool> ExistsAsync(string email, int? organizationId = null)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
            var count = await connection.QuerySingleAsync<int>(sql, new { Email = email });
            return count > 0;
        }

        public async Task<bool> VerifyEmailAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE Users SET IsEmailVerified = TRUE, EmailVerificationToken = NULL, EmailVerificationExpiresAt = NULL, UpdatedAt = NOW() WHERE Id = @Id";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<bool> UpdateEmailVerificationTokenAsync(int id, string? token, DateTime? expiry)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE Users SET EmailVerificationToken = @Token, EmailVerificationExpiresAt = @Expiry, UpdatedAt = NOW() WHERE Id = @Id";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, Token = token, Expiry = expiry });
            return rowsAffected > 0;
        }

        public async Task<bool> UpdatePendingEmailChangeAsync(int id, string? pendingEmail, string? code, DateTime? expiry)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Users
                SET PendingEmail = @PendingEmail,
                    EmailChangeVerificationToken = @Code,
                    EmailChangeVerificationExpiresAt = @Expiry,
                    UpdatedAt = NOW()
                WHERE Id = @Id";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                PendingEmail = pendingEmail,
                Code = code,
                Expiry = expiry
            });

            return rowsAffected > 0;
        }

        public async Task<bool> ConfirmEmailChangeAsync(int id, string newEmail)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Users
                SET Email = @NewEmail,
                    Username = @NewEmail,
                    IsEmailVerified = TRUE,
                    PendingEmail = NULL,
                    EmailChangeVerificationToken = NULL,
                    EmailChangeVerificationExpiresAt = NULL,
                    UpdatedAt = NOW()
                WHERE Id = @Id";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                NewEmail = newEmail
            });

            return rowsAffected > 0;
        }
    }
}
