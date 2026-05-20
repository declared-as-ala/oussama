using System.Threading.Tasks;
using DocApi.Common;

namespace DocApi.Services.Interfaces
{
    public interface INotificationGeneratorService
    {
        Task GenerateAutomaticAlertsForUserAsync(UserContext userContext);
    }
}
