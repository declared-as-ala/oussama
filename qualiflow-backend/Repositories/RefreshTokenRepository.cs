using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public RefreshTokenRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT * FROM RefreshTokens 
                WHERE Token = @Token AND IsRevoked = FALSE AND ExpiresAt > NOW()";
            return await connection.QueryFirstOrDefaultAsync<RefreshToken>(sql, new { Token = token });
        }

        public async Task<int> CreateAsync(RefreshToken refreshToken)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO RefreshTokens (UserId, Token, ExpiresAt, IsRevoked, CreatedAt) 
                VALUES (@UserId, @Token, @ExpiresAt, @IsRevoked, @CreatedAt)
                RETURNING Id;";
            
            return await connection.QuerySingleAsync<int>(sql, refreshToken);
        }

        public async Task<bool> RevokeAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE RefreshTokens SET IsRevoked = TRUE, RevokedAt = NOW() WHERE Id = @Id";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<bool> RevokeByUserIdAsync(int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE RefreshTokens SET IsRevoked = TRUE, RevokedAt = NOW() WHERE UserId = @UserId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { UserId = userId });
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteExpiredAsync()
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM RefreshTokens WHERE ExpiresAt < NOW()";
            var rowsAffected = await connection.ExecuteAsync(sql);
            return rowsAffected > 0;
        }
    }
}
