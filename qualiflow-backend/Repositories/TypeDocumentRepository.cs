using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    // Repository implementation for TypeDocument
    public class TypeDocumentRepository : ITypeDocumentRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public TypeDocumentRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> CreateAsync(TypeDocument entity)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO TypeDocument (Name, Description, CreatedAt, CreatedByUserId) 
                VALUES (@Name, @Description, @CreatedAt, @CreatedByUserId)
                RETURNING Id;";
            
            return await connection.ExecuteScalarAsync<int>(sql, entity);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM TypeDocument WHERE Id = @Id";
            var result = await connection.ExecuteAsync(sql, new { Id = id });
            return result > 0;
        }

        public async Task<IEnumerable<TypeDocument>> GetAllAsync()
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT t.*, 
                       cu.Id as CreatedUserId, cu.Username as CreatedUsername, cu.Email as CreatedEmail, cu.Role as CreatedRole,
                       uu.Id as UpdatedUserId, uu.Username as UpdatedUsername, uu.Email as UpdatedEmail, uu.Role as UpdatedRole
                FROM TypeDocument t
                LEFT JOIN Users cu ON t.CreatedByUserId = cu.Id
                LEFT JOIN Users uu ON t.UpdatedByUserId = uu.Id
                ORDER BY t.CreatedAt DESC";
            
            return await connection.QueryAsync<TypeDocument, User, User, TypeDocument>(
                sql,
                (typeDocument, createdUser, updatedUser) =>
                {
                    typeDocument.CreatedByUser = createdUser;
                    typeDocument.UpdatedByUser = updatedUser;
                    return typeDocument;
                },
                splitOn: "CreatedUserId,UpdatedUserId");
        }

        public async Task<TypeDocument?> GetByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT t.*, 
                       cu.Id as CreatedUserId, cu.Username as CreatedUsername, cu.Email as CreatedEmail, cu.Role as CreatedRole,
                       uu.Id as UpdatedUserId, uu.Username as UpdatedUsername, uu.Email as UpdatedEmail, uu.Role as UpdatedRole
                FROM TypeDocument t
                LEFT JOIN Users cu ON t.CreatedByUserId = cu.Id
                LEFT JOIN Users uu ON t.UpdatedByUserId = uu.Id
                WHERE t.Id = @Id";
            
            var result = await connection.QueryAsync<TypeDocument, User, User, TypeDocument>(
                sql,
                (typeDocument, createdUser, updatedUser) =>
                {
                    typeDocument.CreatedByUser = createdUser;
                    typeDocument.UpdatedByUser = updatedUser;
                    return typeDocument;
                },
                new { Id = id },
                splitOn: "CreatedUserId,UpdatedUserId");
            
            return result.FirstOrDefault();
        }

        public async Task<bool> UpdateAsync(TypeDocument entity)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE TypeDocument 
                SET Name = @Name, Description = @Description, UpdatedAt = @UpdatedAt, UpdatedByUserId = @UpdatedByUserId
                WHERE Id = @Id";
            
            var result = await connection.ExecuteAsync(sql, entity);
            return result > 0;
        }
    }
}
