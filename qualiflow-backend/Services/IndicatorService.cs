using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.Indicators;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;

namespace DocApi.Services
{
    public class IndicatorService : IIndicatorService
    {
        private readonly IIndicatorRepository _indicatorRepository;
        private readonly IIndicatorValueRepository _indicatorValueRepository;
        private readonly IIndicatorAlertRepository _indicatorAlertRepository;
        private readonly IProcessRepository _processRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationEventPublisher _notificationEventPublisher;

        public IndicatorService(
            IIndicatorRepository indicatorRepository,
            IIndicatorValueRepository indicatorValueRepository,
            IIndicatorAlertRepository indicatorAlertRepository,
            IProcessRepository processRepository,
            IUserRepository userRepository,
            INotificationEventPublisher notificationEventPublisher)
        {
            _indicatorRepository = indicatorRepository;
            _indicatorValueRepository = indicatorValueRepository;
            _indicatorAlertRepository = indicatorAlertRepository;
            _processRepository = processRepository;
            _userRepository = userRepository;
            _notificationEventPublisher = notificationEventPublisher;
        }

        public async Task<PagedIndicatorResponse> GetIndicatorsAsync(GetIndicatorsQueryRequest query, UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
            var status = IndicatorConstants.NormalizeStatus(query.Status);
            var frequency = IndicatorConstants.NormalizeMeasurementFrequency(query.MeasurementFrequency);

            if (!string.IsNullOrWhiteSpace(status) && !IndicatorConstants.AllowedStatuses.Contains(status))
            {
                throw new ServiceException("Le statut de l'indicateur est invalide.");
            }

            if (!string.IsNullOrWhiteSpace(frequency) && !IndicatorConstants.AllowedFrequencies.Contains(frequency))
            {
                throw new ServiceException("La frequence de mesure est invalide.");
            }

            var items = await _indicatorRepository.SearchAsync(
                pageNumber,
                pageSize,
                NormalizeSearch(query.Search),
                status,
                query.ProcessId,
                frequency,
                query.ResponsibleUserId,
                query.IsInAlert,
                organizationId);

            var total = await _indicatorRepository.CountSearchAsync(
                NormalizeSearch(query.Search),
                status,
                query.ProcessId,
                frequency,
                query.ResponsibleUserId,
                query.IsInAlert,
                organizationId);

            return new PagedIndicatorResponse
            {
                Total = total,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Items = items.Select(MapToListItemResponse).ToList()
            };
        }

        public async Task<IndicatorDetailsResponse> GetByIdAsync(int id, UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            var details = await _indicatorRepository.GetDetailsByIdAsync(id, organizationId);
            if (details == null)
            {
                throw new NotFoundException("Indicateur introuvable.");
            }

            var values = (await _indicatorValueRepository.GetByIndicatorIdAsync(id, organizationId)).ToList();
            var alerts = (await _indicatorAlertRepository.GetByIndicatorIdAsync(id, organizationId, null, 50)).ToList();
            var latestValue = values
                .OrderByDescending(v => v.MeasuredAt)
                .ThenByDescending(v => v.Id)
                .FirstOrDefault();

            return MapToDetailsResponse(details, values, alerts, latestValue);
        }

        public async Task<IndicatorResponse> CreateAsync(CreateIndicatorRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            var payload = await ValidateIndicatorPayloadAsync(
                request.ProcessId,
                request.Code,
                request.Name,
                request.Description,
                request.CalculationMethod,
                request.Unit,
                request.TargetValue,
                request.AlertThreshold,
                request.MeasurementFrequency,
                request.ResponsibleUserId,
                request.Status,
                organizationId,
                null);

            var entity = new Indicator
            {
                OrganizationId = organizationId,
                ProcessId = payload.Process.Id,
                Code = payload.Code,
                Name = payload.Name,
                Description = payload.Description,
                CalculationMethod = payload.CalculationMethod,
                Unit = payload.Unit,
                TargetValue = payload.TargetValue,
                AlertThreshold = payload.AlertThreshold,
                MeasurementFrequency = payload.MeasurementFrequency,
                ResponsibleUserId = payload.ResponsibleUserId,
                Status = payload.Status,
                CreatedAt = DateTime.UtcNow
            };

            var id = await _indicatorRepository.CreateAsync(entity);

            var created = await _indicatorRepository.GetDetailsByIdAsync(id, organizationId);
            if (created == null)
            {
                throw new NotFoundException("Indicateur introuvable apres creation.");
            }

            return MapToResponse(created);
        }

        public async Task<IndicatorResponse> UpdateAsync(int id, UpdateIndicatorRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            var current = await GetIndicatorOrThrowAsync(id);
            EnsureAccessToOrganization(current, organizationId);

            var payload = await ValidateIndicatorPayloadAsync(
                request.ProcessId,
                request.Code,
                request.Name,
                request.Description,
                request.CalculationMethod,
                request.Unit,
                request.TargetValue,
                request.AlertThreshold,
                request.MeasurementFrequency,
                request.ResponsibleUserId,
                request.Status,
                organizationId,
                id);

            current.ProcessId = payload.Process.Id;
            current.Code = payload.Code;
            current.Name = payload.Name;
            current.Description = payload.Description;
            current.CalculationMethod = payload.CalculationMethod;
            current.Unit = payload.Unit;
            current.TargetValue = payload.TargetValue;
            current.AlertThreshold = payload.AlertThreshold;
            current.MeasurementFrequency = payload.MeasurementFrequency;
            current.ResponsibleUserId = payload.ResponsibleUserId;
            current.Status = payload.Status;
            current.UpdatedAt = DateTime.UtcNow;

            await _indicatorRepository.UpdateAsync(current);
            await ReevaluateAlertStateAsync(current, organizationId, userContext.UserId);

            var updated = await _indicatorRepository.GetDetailsByIdAsync(id, organizationId);
            if (updated == null)
            {
                throw new NotFoundException("Indicateur introuvable apres mise a jour.");
            }

            return MapToResponse(updated);
        }

        public async Task<bool> DeleteAsync(int id, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            var entity = await GetIndicatorOrThrowAsync(id);
            EnsureAccessToOrganization(entity, organizationId);

            var deleted = await _indicatorRepository.DeleteAsync(id, organizationId);
            if (deleted)
            {
            }

            return deleted;
        }

        public async Task<IndicatorResponse> ToggleStatusAsync(int id, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            var entity = await GetIndicatorOrThrowAsync(id);
            EnsureAccessToOrganization(entity, organizationId);

            var currentStatus = IndicatorConstants.NormalizeStatus(entity.Status) ?? IndicatorConstants.StatusActive;
            var nextStatus = string.Equals(currentStatus, IndicatorConstants.StatusActive, StringComparison.OrdinalIgnoreCase)
                ? IndicatorConstants.StatusInactive
                : IndicatorConstants.StatusActive;

            await _indicatorRepository.ToggleStatusAsync(id, organizationId, nextStatus, DateTime.UtcNow);

            var updated = await _indicatorRepository.GetDetailsByIdAsync(id, organizationId);
            if (updated == null)
            {
                throw new NotFoundException("Indicateur introuvable apres changement de statut.");
            }

            return MapToResponse(updated);
        }

        public async Task<IndicatorStatisticsResponse> GetStatisticsAsync(UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            var items = (await _indicatorRepository.GetForStatisticsAsync(organizationId)).ToList();

            var normalized = items.Select(item => new
            {
                Item = item,
                Status = IndicatorConstants.NormalizeStatus(item.Status) ?? IndicatorConstants.StatusActive,
                Frequency = IndicatorConstants.NormalizeMeasurementFrequency(item.MeasurementFrequency) ?? IndicatorConstants.FrequencyMonthly,
                Process = string.IsNullOrWhiteSpace(item.ProcessName)
                    ? $"Process-{item.ProcessId}"
                    : item.ProcessName!.Trim()
            }).ToList();

            return new IndicatorStatisticsResponse
            {
                Total = normalized.Count,
                Active = normalized.Count(x => x.Status == IndicatorConstants.StatusActive),
                Inactive = normalized.Count(x => x.Status == IndicatorConstants.StatusInactive),
                InAlert = normalized.Count(x => x.Item.IsInAlert),
                ByFrequency = normalized
                    .GroupBy(x => x.Frequency)
                    .ToDictionary(group => group.Key, group => group.Count()),
                ByProcess = normalized
                    .GroupBy(x => x.Process)
                    .ToDictionary(group => group.Key, group => group.Count())
            };
        }

        public async Task<List<IndicatorListItemResponse>> GetByProcessAsync(int processId, UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            if (processId <= 0)
            {
                throw new ServiceException("Le processus de rattachement est obligatoire.");
            }

            var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
            {
                throw new NotFoundException("Processus introuvable.");
            }

            if (process.OrganizationId != organizationId)
            {
                throw new ForbiddenException("Le processus n'appartient pas a l'organisation courante.");
            }

            var items = await _indicatorRepository.GetByProcessAsync(processId, organizationId);
            return items.Select(MapToListItemResponse).ToList();
        }

        public async Task<IndicatorChartResponse> GetChartAsync(int id, UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            var details = await _indicatorRepository.GetDetailsByIdAsync(id, organizationId);
            if (details == null)
            {
                throw new NotFoundException("Indicateur introuvable.");
            }

            var values = (await _indicatorValueRepository.GetByIndicatorIdAsync(id, organizationId))
                .OrderBy(v => v.MeasuredAt)
                .ThenBy(v => v.Id)
                .ToList();

            return new IndicatorChartResponse
            {
                Labels = values.Select(v => string.IsNullOrWhiteSpace(v.PeriodLabel)
                    ? v.MeasuredAt.ToString("yyyy-MM-dd")
                    : v.PeriodLabel.Trim()).ToList(),
                Values = values.Select(v => v.MeasuredValue).ToList(),
                TargetValue = details.TargetValue,
                ThresholdValue = details.AlertThreshold
            };
        }

        public async Task<List<IndicatorAlertResponse>> GetAlertsAsync(UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            var alerts = await _indicatorAlertRepository.GetActiveAsync(organizationId);
            return alerts.Select(MapToAlertResponse).ToList();
        }

        public async Task<List<IndicatorValueResponse>> GetValuesAsync(int indicatorId, UserContext userContext)
        {
            EnsureCanRead(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            var indicator = await GetIndicatorOrThrowAsync(indicatorId);
            EnsureAccessToOrganization(indicator, organizationId);

            var values = await _indicatorValueRepository.GetByIndicatorIdAsync(indicatorId, organizationId);
            return values.Select(MapToValueResponse).ToList();
        }

        public async Task<IndicatorValueResponse> CreateValueAsync(int indicatorId, CreateIndicatorValueRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            var indicator = await GetIndicatorOrThrowAsync(indicatorId);
            EnsureAccessToOrganization(indicator, organizationId);

            var payload = await ValidateValuePayloadAsync(
                indicatorId,
                request.PeriodLabel,
                request.MeasuredValue,
                request.Comment,
                request.MeasuredAt,
                organizationId,
                null);

            var entity = new IndicatorValue
            {
                OrganizationId = organizationId,
                IndicatorId = indicatorId,
                PeriodLabel = payload.PeriodLabel,
                MeasuredValue = payload.MeasuredValue,
                Comment = payload.Comment,
                MeasuredAt = payload.MeasuredAt,
                EnteredByUserId = userContext.UserId,
                CreatedAt = DateTime.UtcNow
            };

            var valueId = await _indicatorValueRepository.CreateAsync(entity);
            await ReevaluateAlertStateAsync(indicator, organizationId, userContext.UserId);

            var created = await _indicatorValueRepository.GetByIdAsync(valueId);
            if (created == null)
            {
                throw new NotFoundException("Valeur indicateur introuvable apres creation.");
            }

            return MapToValueResponse(created);
        }

        public async Task<IndicatorValueResponse> UpdateValueAsync(int indicatorId, int valueId, UpdateIndicatorValueRequest request, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            var indicator = await GetIndicatorOrThrowAsync(indicatorId);
            EnsureAccessToOrganization(indicator, organizationId);

            var existing = await _indicatorValueRepository.GetByIdAsync(valueId);
            if (existing == null || existing.OrganizationId != organizationId || existing.IndicatorId != indicatorId)
            {
                throw new NotFoundException("Valeur indicateur introuvable.");
            }

            var payload = await ValidateValuePayloadAsync(
                indicatorId,
                request.PeriodLabel,
                request.MeasuredValue,
                request.Comment,
                request.MeasuredAt,
                organizationId,
                valueId);

            var entity = new IndicatorValue
            {
                Id = existing.Id,
                OrganizationId = existing.OrganizationId,
                IndicatorId = existing.IndicatorId,
                PeriodLabel = payload.PeriodLabel,
                MeasuredValue = payload.MeasuredValue,
                Comment = payload.Comment,
                MeasuredAt = payload.MeasuredAt,
                EnteredByUserId = existing.EnteredByUserId,
                CreatedAt = existing.CreatedAt
            };

            await _indicatorValueRepository.UpdateAsync(entity);
            await ReevaluateAlertStateAsync(indicator, organizationId, userContext.UserId);

            var updated = await _indicatorValueRepository.GetByIdAsync(valueId);
            if (updated == null)
            {
                throw new NotFoundException("Valeur indicateur introuvable apres mise a jour.");
            }

            return MapToValueResponse(updated);
        }

        public async Task<bool> DeleteValueAsync(int indicatorId, int valueId, UserContext userContext)
        {
            EnsureCanWrite(userContext);
            var organizationId = ResolveOrganizationScope(userContext);

            var indicator = await GetIndicatorOrThrowAsync(indicatorId);
            EnsureAccessToOrganization(indicator, organizationId);

            var existing = await _indicatorValueRepository.GetByIdAsync(valueId);
            if (existing == null || existing.OrganizationId != organizationId || existing.IndicatorId != indicatorId)
            {
                throw new NotFoundException("Valeur indicateur introuvable.");
            }

            var deleted = await _indicatorValueRepository.DeleteAsync(valueId, indicatorId, organizationId);
            if (deleted)
            {
                await ReevaluateAlertStateAsync(indicator, organizationId, userContext.UserId);
            }

            return deleted;
        }

        private async Task ReevaluateAlertStateAsync(Indicator indicator, int organizationId, int? triggeredByUserId = null)
        {
            var latestValue = await _indicatorValueRepository.GetLatestByIndicatorIdAsync(indicator.Id, organizationId);
            if (latestValue == null)
            {
                await _indicatorAlertRepository.ResolveOpenByIndicatorAsync(indicator.Id, organizationId, DateTime.UtcNow);
                return;
            }

            var evaluation = IndicatorConstants.EvaluateAlert(latestValue.MeasuredValue, indicator.TargetValue, indicator.AlertThreshold);
            if (evaluation.IsInAlert)
            {
                var hasOpenForCurrentValue = await _indicatorAlertRepository.ExistsOpenForValueAsync(indicator.Id, latestValue.Id, organizationId);
                if (hasOpenForCurrentValue)
                {
                    return;
                }

                await _indicatorAlertRepository.ResolveOpenByIndicatorAsync(indicator.Id, organizationId, DateTime.UtcNow);

                await _indicatorAlertRepository.CreateAsync(new IndicatorAlert
                {
                    OrganizationId = organizationId,
                    IndicatorId = indicator.Id,
                    IndicatorValueId = latestValue.Id,
                    AlertType = evaluation.AlertType ?? IndicatorConstants.AlertTypeBelowThreshold,
                    Message = evaluation.Message ?? "Alerte sur indicateur.",
                    IsResolved = false,
                    CreatedAt = DateTime.UtcNow
                });

                var priority = string.Equals(evaluation.AlertType, IndicatorConstants.AlertTypeBelowThreshold, StringComparison.OrdinalIgnoreCase)
                    ? NotificationConstants.PriorityCritical
                    : NotificationConstants.PriorityHigh;

                await _notificationEventPublisher.PublishToRolesAsync(
                    organizationId,
                    new[] { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE, UserRoles.CHEF_SERVICE, UserRoles.UTILISATEUR },
                    NotificationConstants.TypeIndicatorAlert,
                    NotificationConstants.CategoryWarning,
                    $"Alerte indicateur {indicator.Code}",
                    evaluation.Message ?? $"L'indicateur {indicator.Name} est en alerte.",
                    priority,
                    "INDICATOR",
                    indicator.Id.ToString(),
                    $"/indicators/{indicator.Id}",
                    triggeredByUserId);

                await _notificationEventPublisher.PublishToUserAsync(
                    organizationId,
                    indicator.ResponsibleUserId,
                    NotificationConstants.TypeIndicatorAlert,
                    NotificationConstants.CategoryWarning,
                    $"Alerte indicateur {indicator.Code}",
                    evaluation.Message ?? $"L'indicateur {indicator.Name} est en alerte.",
                    priority,
                    "INDICATOR",
                    indicator.Id.ToString(),
                    $"/indicators/{indicator.Id}",
                    triggeredByUserId);

                return;
            }

            await _indicatorAlertRepository.ResolveOpenByIndicatorAsync(indicator.Id, organizationId, DateTime.UtcNow);
        }

        private async Task<IndicatorValidatedPayload> ValidateIndicatorPayloadAsync(
            int processId,
            string code,
            string name,
            string? description,
            string? calculationMethod,
            string? unit,
            decimal targetValue,
            decimal alertThreshold,
            string measurementFrequency,
            int responsibleUserId,
            string status,
            int organizationId,
            int? excludeIndicatorId)
        {
            if (processId <= 0)
            {
                throw new ServiceException("Le rattachement a un processus est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ServiceException("Le code de l'indicateur est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ServiceException("Le nom de l'indicateur est obligatoire.");
            }

            if (responsibleUserId <= 0)
            {
                throw new ServiceException("Le responsable de l'indicateur est obligatoire.");
            }

            var normalizedFrequency = IndicatorConstants.NormalizeMeasurementFrequency(measurementFrequency);
            if (string.IsNullOrWhiteSpace(normalizedFrequency) || !IndicatorConstants.AllowedFrequencies.Contains(normalizedFrequency))
            {
                throw new ServiceException("La frequence de mesure est invalide.");
            }

            var normalizedStatus = IndicatorConstants.NormalizeStatus(status);
            if (string.IsNullOrWhiteSpace(normalizedStatus) || !IndicatorConstants.AllowedStatuses.Contains(normalizedStatus))
            {
                throw new ServiceException("Le statut de l'indicateur est invalide.");
            }

            if (targetValue < 0)
            {
                throw new ServiceException("La cible de l'indicateur doit etre positive ou nulle.");
            }

            if (alertThreshold < 0)
            {
                throw new ServiceException("Le seuil d'alerte doit etre positif ou nul.");
            }

            var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
            {
                throw new ServiceException("Le processus selectionne est introuvable.");
            }

            if (process.OrganizationId != organizationId)
            {
                throw new ForbiddenException("Le processus doit appartenir a la meme organisation.");
            }

            var responsible = await _userRepository.GetByIdAsync(responsibleUserId);
            if (responsible == null || !responsible.IsActive)
            {
                throw new ServiceException("Le responsable selectionne est invalide ou inactif.");
            }

            if (responsible.OrganizationId != organizationId)
            {
                throw new ForbiddenException("Le responsable doit appartenir a la meme organisation.");
            }

            var normalizedCode = code.Trim().ToUpperInvariant();
            var alreadyExists = await _indicatorRepository.ExistsCodeAsync(organizationId, normalizedCode, excludeIndicatorId);
            if (alreadyExists)
            {
                throw new ServiceException("Ce code indicateur existe deja dans l'organisation.");
            }

            return new IndicatorValidatedPayload
            {
                Process = process,
                Code = normalizedCode,
                Name = name.Trim(),
                Description = NormalizeNullable(description),
                CalculationMethod = NormalizeNullable(calculationMethod),
                Unit = NormalizeNullable(unit),
                TargetValue = targetValue,
                AlertThreshold = alertThreshold,
                MeasurementFrequency = normalizedFrequency,
                ResponsibleUserId = responsibleUserId,
                Status = normalizedStatus
            };
        }

        private async Task<IndicatorValueValidatedPayload> ValidateValuePayloadAsync(
            int indicatorId,
            string periodLabel,
            decimal measuredValue,
            string? comment,
            DateTime measuredAt,
            int organizationId,
            int? excludeValueId)
        {
            if (string.IsNullOrWhiteSpace(periodLabel))
            {
                throw new ServiceException("Le libelle de periode est obligatoire.");
            }

            if (measuredAt == default)
            {
                throw new ServiceException("La date de mesure est obligatoire.");
            }

            var normalizedPeriod = periodLabel.Trim();
            var alreadyExists = await _indicatorValueRepository.ExistsPeriodAsync(indicatorId, organizationId, normalizedPeriod, excludeValueId);
            if (alreadyExists)
            {
                throw new ServiceException("Une valeur existe deja pour cette periode.");
            }

            return new IndicatorValueValidatedPayload
            {
                PeriodLabel = normalizedPeriod,
                MeasuredValue = measuredValue,
                Comment = NormalizeNullable(comment),
                MeasuredAt = measuredAt
            };
        }

        private async Task<Indicator> GetIndicatorOrThrowAsync(int id)
        {
            var indicator = await _indicatorRepository.GetByIdAsync(id);
            if (indicator == null)
            {
                throw new NotFoundException("Indicateur introuvable.");
            }

            indicator.Status = IndicatorConstants.NormalizeStatus(indicator.Status) ?? IndicatorConstants.StatusActive;
            indicator.MeasurementFrequency = IndicatorConstants.NormalizeMeasurementFrequency(indicator.MeasurementFrequency) ?? IndicatorConstants.FrequencyMonthly;
            return indicator;
        }

        private static void EnsureCanRead(UserContext userContext)
        {
            if (!userContext.CanReadIndicators)
            {
                throw new ForbiddenException("Vous n'avez pas les droits de lecture sur les indicateurs.");
            }
        }

        private static void EnsureCanWrite(UserContext userContext)
        {
            if (!userContext.CanWriteIndicators)
            {
                throw new ForbiddenException("Vous n'avez pas les droits d'ecriture sur les indicateurs.");
            }
        }

        private static int ResolveOrganizationScope(UserContext userContext)
        {
            if (!userContext.OrganizationId.HasValue)
            {
                throw new ForbiddenException("Organisation introuvable dans le token utilisateur.");
            }

            return userContext.OrganizationId.Value;
        }

        private static void EnsureAccessToOrganization(Indicator indicator, int organizationId)
        {
            if (indicator.OrganizationId != organizationId)
            {
                throw new ForbiddenException("Acces refuse a cet indicateur.");
            }
        }

        private static string? NormalizeSearch(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static string? NormalizeNullable(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static IndicatorListItemResponse MapToListItemResponse(IndicatorListItemData data)
        {
            return new IndicatorListItemResponse
            {
                Id = data.Id,
                ProcessId = data.ProcessId,
                ProcessName = data.ProcessName,
                Code = data.Code,
                Name = data.Name,
                Unit = data.Unit,
                TargetValue = data.TargetValue,
                AlertThreshold = data.AlertThreshold,
                LatestValue = data.LatestValue,
                LatestMeasuredAt = data.LatestMeasuredAt,
                Status = IndicatorConstants.NormalizeStatus(data.Status) ?? IndicatorConstants.StatusActive,
                ResponsibleFullName = data.ResponsibleFullName,
                IsInAlert = data.IsInAlert,
                CreatedAt = data.CreatedAt
            };
        }

        private static IndicatorResponse MapToResponse(IndicatorDetailsData data)
        {
            return new IndicatorResponse
            {
                Id = data.Id,
                OrganizationId = data.OrganizationId,
                ProcessId = data.ProcessId,
                ProcessCode = data.ProcessCode,
                ProcessName = data.ProcessName,
                Code = data.Code,
                Name = data.Name,
                Description = data.Description,
                CalculationMethod = data.CalculationMethod,
                Unit = data.Unit,
                TargetValue = data.TargetValue,
                AlertThreshold = data.AlertThreshold,
                MeasurementFrequency = IndicatorConstants.NormalizeMeasurementFrequency(data.MeasurementFrequency) ?? IndicatorConstants.FrequencyMonthly,
                ResponsibleUserId = data.ResponsibleUserId,
                ResponsibleFullName = data.ResponsibleFullName,
                Status = IndicatorConstants.NormalizeStatus(data.Status) ?? IndicatorConstants.StatusActive,
                LatestValue = data.LatestValue,
                LatestMeasuredAt = data.LatestMeasuredAt,
                IsInAlert = data.IsInAlert,
                CreatedAt = data.CreatedAt,
                UpdatedAt = data.UpdatedAt
            };
        }

        private static IndicatorValueResponse MapToValueResponse(IndicatorValueData value)
        {
            return new IndicatorValueResponse
            {
                Id = value.Id,
                IndicatorId = value.IndicatorId,
                PeriodLabel = value.PeriodLabel,
                MeasuredValue = value.MeasuredValue,
                Comment = value.Comment,
                MeasuredAt = value.MeasuredAt,
                EnteredByUserId = value.EnteredByUserId,
                EnteredByFullName = value.EnteredByFullName,
                CreatedAt = value.CreatedAt
            };
        }

        private static IndicatorAlertResponse MapToAlertResponse(IndicatorAlertData alert)
        {
            return new IndicatorAlertResponse
            {
                IndicatorId = alert.IndicatorId,
                IndicatorCode = alert.IndicatorCode,
                IndicatorName = alert.IndicatorName,
                Message = alert.Message,
                MeasuredValue = alert.MeasuredValue,
                TargetValue = alert.TargetValue,
                AlertThreshold = alert.AlertThreshold,
                MeasuredAt = alert.MeasuredAt
            };
        }

        private static IndicatorDetailsResponse MapToDetailsResponse(
            IndicatorDetailsData details,
            IEnumerable<IndicatorValueData> values,
            IEnumerable<IndicatorAlertData> alerts,
            IndicatorValueData? latestValue)
        {
            var indicator = MapToResponse(details);
            return new IndicatorDetailsResponse
            {
                Indicator = indicator,
                Process = new IndicatorLinkedProcessResponse
                {
                    Id = details.ProcessId,
                    Code = details.ProcessCode,
                    Name = details.ProcessName
                },
                Responsible = new IndicatorResponsibleResponse
                {
                    Id = details.ResponsibleUserId,
                    FullName = details.ResponsibleFullName,
                    Email = details.ResponsibleEmail
                },
                LatestValue = latestValue != null ? MapToValueResponse(latestValue) : null,
                IsInAlert = indicator.IsInAlert,
                ValuesHistory = values.Select(MapToValueResponse).ToList(),
                Alerts = alerts.Select(MapToAlertResponse).ToList()
            };
        }

        private sealed class IndicatorValidatedPayload
        {
            public required Process Process { get; set; }
            public required string Code { get; set; }
            public required string Name { get; set; }
            public string? Description { get; set; }
            public string? CalculationMethod { get; set; }
            public string? Unit { get; set; }
            public decimal TargetValue { get; set; }
            public decimal AlertThreshold { get; set; }
            public required string MeasurementFrequency { get; set; }
            public int ResponsibleUserId { get; set; }
            public required string Status { get; set; }
        }

        private sealed class IndicatorValueValidatedPayload
        {
            public required string PeriodLabel { get; set; }
            public decimal MeasuredValue { get; set; }
            public string? Comment { get; set; }
            public DateTime MeasuredAt { get; set; }
        }
    }
}
