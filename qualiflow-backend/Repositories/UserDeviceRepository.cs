using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class UserDeviceRepository : IUserDeviceRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserDeviceRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> UpsertAsync(UserDevice device)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO UserDevices
                    (UserId, DeviceToken, Platform, DeviceName, IsActive, CreatedAt, LastSeenAt)
                VALUES
                    (@UserId, @DeviceToken, @Platform, @DeviceName, TRUE, NOW(), NOW())
                ON CONFLICT (UserId, DeviceToken)
                DO UPDATE SET
                    Platform = EXCLUDED.Platform,
                    DeviceName = EXCLUDED.DeviceName,
                    IsActive = TRUE,
                    LastSeenAt = NOW()
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, device);
        }

        public async Task<bool> DeactivateAsync(int userId, string deviceToken)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE UserDevices
                SET IsActive = FALSE
                WHERE UserId = @UserId
                  AND DeviceToken = @DeviceToken
                  AND IsActive = TRUE;";

            var affected = await connection.ExecuteAsync(sql, new { UserId = userId, DeviceToken = deviceToken });
            return affected > 0;
        }

        public async Task<bool> DeactivateByTokenAsync(string deviceToken)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE UserDevices
                SET IsActive = FALSE
                WHERE DeviceToken = @DeviceToken
                  AND IsActive = TRUE;";

            var affected = await connection.ExecuteAsync(sql, new { DeviceToken = deviceToken });
            return affected > 0;
        }

        public async Task<IEnumerable<UserDevice>> GetActiveByUserIdAsync(int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT *
                FROM UserDevices
                WHERE UserId = @UserId
                  AND IsActive = TRUE
                ORDER BY LastSeenAt DESC NULLS LAST, Id DESC;";

            return await connection.QueryAsync<UserDevice>(sql, new { UserId = userId });
        }
    }
}
