using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class PasswordResetTokenRepository : IPasswordResetTokenRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public PasswordResetTokenRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<PasswordResetToken?> GetByTokenAsync(string token)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT * FROM PasswordResetTokens 
                WHERE Token = @Token AND Used = FALSE AND ExpiresAt > NOW()";
            return await connection.QueryFirstOrDefaultAsync<PasswordResetToken>(sql, new { Token = token });
        }

        public async Task<PasswordResetToken?> GetByUserAndTokenAsync(int userId, string token)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT * FROM PasswordResetTokens
                WHERE UserId = @UserId
                  AND Token = @Token
                  AND Used = FALSE
                  AND ExpiresAt > NOW()
                ORDER BY CreatedAt DESC
                LIMIT 1;";
            return await connection.QueryFirstOrDefaultAsync<PasswordResetToken>(sql, new { UserId = userId, Token = token });
        }

        public async Task<int> CreateAsync(PasswordResetToken token)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO PasswordResetTokens (UserId, Token, ExpiresAt, Used, CreatedAt) 
                VALUES (@UserId, @Token, @ExpiresAt, @Used, @CreatedAt)
                RETURNING Id;";
            
            return await connection.QuerySingleAsync<int>(sql, token);
        }

        public async Task<bool> MarkAsUsedAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE PasswordResetTokens SET Used = TRUE WHERE Id = @Id";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<bool> RevokeActiveByUserIdAsync(int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE PasswordResetTokens
                SET Used = TRUE
                WHERE UserId = @UserId
                  AND Used = FALSE
                  AND ExpiresAt > NOW();";
            var rowsAffected = await connection.ExecuteAsync(sql, new { UserId = userId });
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteExpiredAsync()
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM PasswordResetTokens WHERE ExpiresAt < NOW()";
            var rowsAffected = await connection.ExecuteAsync(sql);
            return rowsAffected > 0;
        }
    }
}
