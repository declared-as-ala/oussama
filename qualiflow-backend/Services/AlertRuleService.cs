using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.Notifications;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;

namespace DocApi.Services
{
    public class AlertRuleService : IAlertRuleService
    {
        private readonly IAlertRuleRepository _alertRuleRepository;

        public AlertRuleService(IAlertRuleRepository alertRuleRepository)
        {
            _alertRuleRepository = alertRuleRepository;
        }

        public async Task<List<AlertRuleResponse>> GetAllAsync(UserContext userContext)
        {
            EnsureCanManage(userContext);
            var organizationId = GetOrganizationScope(userContext);
            var rules = await _alertRuleRepository.GetAllAsync(organizationId);
            return rules.Select(MapToResponse).ToList();
        }

        public async Task<AlertRuleResponse> GetByIdAsync(int id, UserContext userContext)
        {
            EnsureCanManage(userContext);
            var organizationId = GetOrganizationScope(userContext);
            var rule = await GetRuleOrThrowAsync(id);
            EnsureRuleAccess(rule, organizationId);
            return MapToResponse(rule);
        }

        public async Task<AlertRuleResponse> CreateAsync(CreateAlertRuleRequest request, UserContext userContext)
        {
            EnsureCanManage(userContext);
            var organizationId = GetOrganizationScope(userContext);

            await ValidatePayloadAsync(request.Code, request.Name, request.EntityType, request.TriggerType, organizationId, null);

            var entity = new AlertRule
            {
                OrganizationId = organizationId,
                Code = request.Code.Trim().ToUpperInvariant(),
                Name = request.Name.Trim(),
                Description = NormalizeNullable(request.Description),
                EntityType = request.EntityType.Trim().ToUpperInvariant(),
                TriggerType = request.TriggerType.Trim().ToUpperInvariant(),
                IsActive = request.IsActive,
                ThresholdValue = request.ThresholdValue,
                CreatedAt = DateTime.UtcNow
            };

            var id = await _alertRuleRepository.CreateAsync(entity);
            entity.Id = id;
            return MapToResponse(entity);
        }

        public async Task<AlertRuleResponse> UpdateAsync(int id, UpdateAlertRuleRequest request, UserContext userContext)
        {
            EnsureCanManage(userContext);
            var organizationId = GetOrganizationScope(userContext);
            var existing = await GetRuleOrThrowAsync(id);
            EnsureRuleAccess(existing, organizationId);

            await ValidatePayloadAsync(request.Code, request.Name, request.EntityType, request.TriggerType, organizationId, id);

            existing.Code = request.Code.Trim().ToUpperInvariant();
            existing.Name = request.Name.Trim();
            existing.Description = NormalizeNullable(request.Description);
            existing.EntityType = request.EntityType.Trim().ToUpperInvariant();
            existing.TriggerType = request.TriggerType.Trim().ToUpperInvariant();
            existing.IsActive = request.IsActive;
            existing.ThresholdValue = request.ThresholdValue;
            existing.UpdatedAt = DateTime.UtcNow;

            await _alertRuleRepository.UpdateAsync(existing);
            return MapToResponse(existing);
        }

        public async Task<AlertRuleResponse> ToggleStatusAsync(int id, UserContext userContext)
        {
            EnsureCanManage(userContext);
            var organizationId = GetOrganizationScope(userContext);
            var existing = await GetRuleOrThrowAsync(id);
            EnsureRuleAccess(existing, organizationId);

            var next = !existing.IsActive;
            await _alertRuleRepository.ToggleStatusAsync(id, next);
            existing.IsActive = next;
            existing.UpdatedAt = DateTime.UtcNow;

            return MapToResponse(existing);
        }

        private static void EnsureCanManage(UserContext userContext)
        {
            if (!userContext.CanManageAlertRules)
            {
                throw new ForbiddenException("Vous n'avez pas les droits de gestion des regles d'alerte.");
            }
        }

        private static int GetOrganizationScope(UserContext userContext)
        {
            if (!userContext.OrganizationId.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable dans le token utilisateur.");
            }

            return userContext.OrganizationId.Value;
        }

        private static void EnsureRuleAccess(AlertRule rule, int organizationId)
        {
            if (rule.OrganizationId.HasValue && rule.OrganizationId.Value != organizationId)
            {
                throw new ForbiddenException("Acces refuse a cette regle d'alerte.");
            }
        }

        private async Task<AlertRule> GetRuleOrThrowAsync(int id)
        {
            var rule = await _alertRuleRepository.GetByIdAsync(id);
            if (rule == null)
            {
                throw new NotFoundException("Regle d'alerte introuvable.");
            }

            return rule;
        }

        private async Task ValidatePayloadAsync(
            string code,
            string name,
            string entityType,
            string triggerType,
            int organizationId,
            int? excludeId)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ServiceException("Le code de la regle d'alerte est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ServiceException("Le nom de la regle d'alerte est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(entityType))
            {
                throw new ServiceException("Le type d'entite de la regle d'alerte est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(triggerType))
            {
                throw new ServiceException("Le type de declenchement de la regle d'alerte est obligatoire.");
            }

            var exists = await _alertRuleRepository.ExistsCodeAsync(organizationId, code.Trim().ToUpperInvariant(), excludeId);
            if (exists)
            {
                throw new ServiceException("Ce code de regle d'alerte existe deja.");
            }
        }

        private static AlertRuleResponse MapToResponse(AlertRule rule)
        {
            return new AlertRuleResponse
            {
                Id = rule.Id,
                OrganizationId = rule.OrganizationId,
                Code = rule.Code,
                Name = rule.Name,
                Description = rule.Description,
                EntityType = rule.EntityType,
                TriggerType = rule.TriggerType,
                IsActive = rule.IsActive,
                ThresholdValue = rule.ThresholdValue,
                CreatedAt = rule.CreatedAt,
                UpdatedAt = rule.UpdatedAt
            };
        }

        private static string? NormalizeNullable(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }
    }
}
