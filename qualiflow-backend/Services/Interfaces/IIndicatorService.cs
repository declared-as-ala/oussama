using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Indicators;

namespace DocApi.Services.Interfaces
{
    public interface IIndicatorService
    {
        Task<PagedIndicatorResponse> GetIndicatorsAsync(GetIndicatorsQueryRequest query, UserContext userContext);
        Task<IndicatorDetailsResponse> GetByIdAsync(int id, UserContext userContext);
        Task<IndicatorResponse> CreateAsync(CreateIndicatorRequest request, UserContext userContext);
        Task<IndicatorResponse> UpdateAsync(int id, UpdateIndicatorRequest request, UserContext userContext);
        Task<bool> DeleteAsync(int id, UserContext userContext);
        Task<IndicatorResponse> ToggleStatusAsync(int id, UserContext userContext);
        Task<IndicatorStatisticsResponse> GetStatisticsAsync(UserContext userContext);
        Task<List<IndicatorListItemResponse>> GetByProcessAsync(int processId, UserContext userContext);
        Task<IndicatorChartResponse> GetChartAsync(int id, UserContext userContext);
        Task<List<IndicatorAlertResponse>> GetAlertsAsync(UserContext userContext);
        Task<List<IndicatorValueResponse>> GetValuesAsync(int indicatorId, UserContext userContext);
        Task<IndicatorValueResponse> CreateValueAsync(int indicatorId, CreateIndicatorValueRequest request, UserContext userContext);
        Task<IndicatorValueResponse> UpdateValueAsync(int indicatorId, int valueId, UpdateIndicatorValueRequest request, UserContext userContext);
        Task<bool> DeleteValueAsync(int indicatorId, int valueId, UserContext userContext);
        Task<List<IndicatorActionLogResponse>> GetActionLogsAsync(int indicatorId, UserContext userContext);
        Task<bool> DeleteActionLogAsync(int logId, UserContext userContext);
    }
}
