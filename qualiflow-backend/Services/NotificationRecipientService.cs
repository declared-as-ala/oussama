using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.Domain.Enums;
using DocApi.DTOs.Notifications;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;

namespace DocApi.Services
{
    public class NotificationRecipientService : INotificationRecipientService
    {
        private readonly INotificationRuleRepository _notificationRuleRepository;
        private readonly IUserRepository _userRepository;

        public NotificationRecipientService(
            INotificationRuleRepository notificationRuleRepository,
            IUserRepository userRepository)
        {
            _notificationRuleRepository = notificationRuleRepository;
            _userRepository = userRepository;
        }

        public async Task<IReadOnlyList<NotificationRecipientResponse>> GetRecipientsAsync(
            int organizationId,
            NotificationEventType eventType,
            int? documentId)
        {
            var rules = await _notificationRuleRepository.GetByEventTypeAsync(organizationId, eventType.ToString());
            var effectiveRules = rules.Count > 0
                ? rules
                : BuildDefaultRules(eventType);

            var recipients = new Dictionary<int, NotificationRecipientResponse>();

            foreach (var rule in effectiveRules.Where(r => r.IsActive))
            {
                if (!Enum.TryParse<RoleType>(rule.RoleType, ignoreCase: true, out var roleType))
                {
                    continue;
                }

                var legacyRole = WorkflowRoleMapper.ToLegacyRole(roleType);
                var users = await _userRepository.GetActiveByOrganizationAndRolesAsync(organizationId, new[] { legacyRole });

                foreach (var user in users)
                {
                    if (!recipients.ContainsKey(user.Id))
                    {
                        recipients[user.Id] = new NotificationRecipientResponse
                        {
                            UserId = user.Id,
                            FullName = $"{user.FirstName} {user.LastName}".Trim(),
                            Email = user.Email,
                            Role = user.Role,
                            RoleType = roleType.ToString()
                        };
                    }
                }
            }

            return recipients.Values
                .OrderBy(r => r.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IReadOnlyList<NotificationRule> BuildDefaultRules(NotificationEventType eventType)
        {
            var roleTypes = eventType switch
            {
                NotificationEventType.DocumentCreated => new[] { RoleType.QualityManager, RoleType.DepartmentManager },
                NotificationEventType.DocumentSubmitted => new[] { RoleType.DepartmentManager, RoleType.QualityManager },
                NotificationEventType.DocumentApproved => new[] { RoleType.Employee, RoleType.DepartmentManager, RoleType.QualityManager },
                NotificationEventType.DocumentRejected => new[] { RoleType.Employee, RoleType.DepartmentManager, RoleType.QualityManager },
                NotificationEventType.DocumentArchived => new[] { RoleType.Employee, RoleType.DepartmentManager, RoleType.QualityManager },
                NotificationEventType.DocumentExpiring30 => new[] { RoleType.Employee, RoleType.DepartmentManager, RoleType.QualityManager },
                NotificationEventType.DocumentExpiring7 => new[] { RoleType.Employee, RoleType.DepartmentManager, RoleType.QualityManager },
                NotificationEventType.DocumentExpiring1 => new[] { RoleType.Employee, RoleType.DepartmentManager, RoleType.QualityManager },
                NotificationEventType.DocumentExpired => new[] { RoleType.Employee, RoleType.DepartmentManager, RoleType.QualityManager },
                _ => new[] { RoleType.QualityManager }
            };

            return roleTypes.Select(roleType => new NotificationRule
            {
                EventType = eventType.ToString(),
                RoleType = roleType.ToString(),
                EmailEnabled = true,
                InAppEnabled = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }).ToList();
        }
    }
}
