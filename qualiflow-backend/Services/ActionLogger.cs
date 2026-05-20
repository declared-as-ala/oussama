using System;
using System.Threading.Tasks;
using DocApi.Domain.Entities;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;

namespace DocApi.Services
{
    public class ActionLogger : IActionLogger
    {
        private readonly IActionLogRepository _actionLogRepository;

        public ActionLogger(IActionLogRepository actionLogRepository)
        {
            _actionLogRepository = actionLogRepository;
        }

        public async Task LogActionAsync(int organizationId, int userId, string actorName, string module, string actionType, string title, string? description = null)
        {
            var log = new ActionLog
            {
                OrganizationId = organizationId,
                PerformedByUserId = userId,
                ActorName = actorName,
                Module = module,
                ActionType = actionType,
                Title = title,
                Description = description ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _actionLogRepository.CreateAsync(log);
        }
    }
}
