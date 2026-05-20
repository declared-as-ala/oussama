using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;  // 🐘 PostgreSQL instead of MySQL

namespace DocApi.Infrastructure
{
    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public DbConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new System.ArgumentNullException("DefaultConnection string is not configured.");
        }

        public IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);  // 🐘 PostgreSQL
        }
    }
}
