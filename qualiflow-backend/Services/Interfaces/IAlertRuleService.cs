using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Notifications;

namespace DocApi.Services.Interfaces
{
    public interface IAlertRuleService
    {
        Task<List<AlertRuleResponse>> GetAllAsync(UserContext userContext);
        Task<AlertRuleResponse> GetByIdAsync(int id, UserContext userContext);
        Task<AlertRuleResponse> CreateAsync(CreateAlertRuleRequest request, UserContext userContext);
        Task<AlertRuleResponse> UpdateAsync(int id, UpdateAlertRuleRequest request, UserContext userContext);
        Task<AlertRuleResponse> ToggleStatusAsync(int id, UserContext userContext);
    }
}
