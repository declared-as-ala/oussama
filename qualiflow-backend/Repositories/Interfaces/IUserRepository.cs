using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<IReadOnlyList<User>> GetByEmailAccountsAsync(string email);
        Task<User?> GetByEmailAndOrganizationAsync(string email, int organizationId);
        Task<User?> GetByPhoneAsync(string phone);
        Task<IReadOnlyList<User>> GetByPhoneAccountsAsync(string phone);
        Task<User?> GetByVerificationTokenAsync(string token);
        Task<IEnumerable<User>> GetAllAsync();
        Task<IEnumerable<User>> GetByOrganizationIdAsync(int organizationId, int page = 1, int pageSize = 10);
        Task<IEnumerable<User>> GetByIdsAsync(int organizationId, IEnumerable<int> ids);
        Task<IEnumerable<User>> GetActiveByOrganizationAndRolesAsync(int organizationId, IEnumerable<string> roles);
        Task<IEnumerable<User>> SearchAsync(string? searchTerm, int? organizationId, int page = 1, int pageSize = 10);
        Task<int> GetTotalCountAsync();
        Task<int> GetCountByOrganizationAsync(int organizationId);
        Task<int> GetSearchCountAsync(string? searchTerm, int? organizationId);
        Task<IEnumerable<User>> GetUsersWithNoProcessAsync(int organizationId, int page = 1, int pageSize = 10);
        Task<int> GetUsersWithNoProcessCountAsync(int organizationId);
        Task<int> CreateAsync(User user);
        Task<bool> UpdateAsync(User user);
        Task<bool> UpdateProfileAsync(int id, string firstName, string lastName, DateTime? birthDate, string? phone, string? city, string? nationality, string preferredLanguage, DateTime updatedAt);
        Task<bool> UpdateProfilePhotoPathAsync(int id, string? profilePhotoPath, DateTime updatedAt);
        Task<bool> ToggleStatusAsync(int id, bool isActive);
        Task<bool> UpdatePasswordAsync(int id, string passwordHash);
        Task<bool> UpdateLastLoginAsync(int id);
        Task<bool> DeleteAsync(int id);
        Task<bool> HardDeleteAsync(int id);
        Task<bool> ExistsAsync(string email, int? organizationId = null);
        Task<bool> VerifyEmailAsync(int id);
        Task<bool> UpdateEmailVerificationTokenAsync(int id, string? token, DateTime? expiry);
        Task<bool> UpdatePendingEmailChangeAsync(int id, string? pendingEmail, string? code, DateTime? expiry);
        Task<bool> ConfirmEmailChangeAsync(int id, string newEmail);
    }
}
