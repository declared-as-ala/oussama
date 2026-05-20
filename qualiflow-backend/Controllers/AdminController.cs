using Microsoft.AspNetCore.Mvc;
using DocApi.Infrastructure;
using Dapper;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AdminController(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        [HttpPost("update-password-hashes")]
        public async Task<IActionResult> UpdatePasswordHashes()
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                
                // Générer les vrais hashes BCrypt
                var adminHash = BCrypt.Net.BCrypt.HashPassword("admin123");
                var userHash = BCrypt.Net.BCrypt.HashPassword("user123");
                var managerHash = BCrypt.Net.BCrypt.HashPassword("manager123");

                // Mettre à jour les hashes dans la base
                var adminResult = await connection.ExecuteAsync(
                    "UPDATE Users SET PasswordHash = @Hash WHERE Username = @Username",
                    new { Hash = adminHash, Username = "admin" });

                var userResult = await connection.ExecuteAsync(
                    "UPDATE Users SET PasswordHash = @Hash WHERE Username = @Username",
                    new { Hash = userHash, Username = "user1" });

                var managerResult = await connection.ExecuteAsync(
                    "UPDATE Users SET PasswordHash = @Hash WHERE Username = @Username",
                    new { Hash = managerHash, Username = "manager" });

                return Ok(new
                {
                    message = "Password hashes updated successfully",
                    results = new
                    {
                        admin = adminResult > 0 ? "Updated" : "Not found",
                        user1 = userResult > 0 ? "Updated" : "Not found", 
                        manager = managerResult > 0 ? "Updated" : "Not found"
                    },
                    hashes = new
                    {
                        admin = adminHash,
                        user1 = userHash,
                        manager = managerHash
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error updating hashes", error = ex.Message });
            }
        }

        [HttpGet("verify-users")]
        public async Task<IActionResult> VerifyUsers()
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                
                var users = await connection.QueryAsync(
                    "SELECT Username, Email, Role, LEFT(PasswordHash, 30) as PasswordHashPreview FROM Users WHERE IsActive = TRUE");

                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error retrieving users", error = ex.Message });
            }
        }

        [HttpGet("test-auth")]
        [Authorize]
        public IActionResult TestAuth()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            
            return Ok(new
            {
                message = "Authorization working!",
                userId = userId,
                username = username,
                role = role,
                claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
        }
    }
}
