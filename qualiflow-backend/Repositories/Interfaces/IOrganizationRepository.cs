using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;
using DocApi.DTOs.Organizations;
using System;

namespace DocApi.Repositories.Interfaces
{
    public interface IOrganizationRepository
    {
        Task<Organization?> GetByIdAsync(int id);
        Task<Organization?> GetByCodeAsync(string code);
        Task<Organization?> GetByNameAsync(string name);
        Task<IEnumerable<Organization>> GetAllAsync(int page, int pageSize);
        Task<int> GetTotalCountAsync();
        Task<IEnumerable<OrganizationListItem>> SearchAsync(OrganizationListQueryParameters query);
        Task<int> CountSearchAsync(OrganizationListQueryParameters query);
        Task<OrganizationDetails?> GetDetailsAsync(int id);
        Task<IEnumerable<OrganizationAdmin>> GetAdminsAsync(int organizationId);
        Task<int> CreateAsync(Organization organization);
        Task<bool> UpdateAsync(Organization organization);
        Task<bool> UpdateLogoPathAsync(int id, string? logoPath);
        Task<bool> ToggleStatusAsync(int id, string status);
        Task<bool> DeleteAsync(int id);
        Task<IReadOnlyList<Organization>> DecrementSubscriptionDaysAsync(DateTime utcNow);
        Task<IReadOnlyList<Organization>> GetActiveExpiredSubscriptionsAsync();
        Task<bool> MarkSubscriptionExpiryAlertSentAsync(int id, bool sent = true);
    }
}
