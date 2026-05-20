using System.Threading.Tasks;

namespace DocApi.Services.Interfaces
{
    public interface IActionLogger
    {
        Task LogActionAsync(int organizationId, int userId, string actorName, string module, string actionType, string title, string? description = null);
    }
}
