using System;
using System.Collections.Generic;

namespace DocApi.Common
{
    public static class IndicatorConstants
    {
        public const string StatusActive = "ACTIF";
        public const string StatusInactive = "INACTIF";

        public const string FrequencyDaily = "QUOTIDIEN";
        public const string FrequencyWeekly = "HEBDOMADAIRE";
        public const string FrequencyMonthly = "MENSUEL";
        public const string FrequencyQuarterly = "TRIMESTRIEL";
        public const string FrequencyYearly = "ANNUEL";

        public const string AlertTypeBelowThreshold = "BELOW_THRESHOLD";
        public const string AlertTypeBelowTarget = "BELOW_TARGET";

        public static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            StatusActive,
            StatusInactive
        };

        public static readonly HashSet<string> AllowedFrequencies = new(StringComparer.OrdinalIgnoreCase)
        {
            FrequencyDaily,
            FrequencyWeekly,
            FrequencyMonthly,
            FrequencyQuarterly,
            FrequencyYearly
        };

        public static readonly HashSet<string> AllowedAlertTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            AlertTypeBelowThreshold,
            AlertTypeBelowTarget
        };

        public static string? NormalizeStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim().ToUpperInvariant();
        }

        public static string? NormalizeMeasurementFrequency(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim().ToUpperInvariant();
        }

        public static AlertEvaluation EvaluateAlert(decimal measuredValue, decimal targetValue, decimal alertThreshold)
        {
            if (measuredValue < alertThreshold)
            {
                return new AlertEvaluation
                {
                    IsInAlert = true,
                    AlertType = AlertTypeBelowThreshold,
                    Message = $"Valeur mesuree ({measuredValue:0.##}) inferieure au seuil d'alerte ({alertThreshold:0.##})."
                };
            }

            if (measuredValue < targetValue)
            {
                return new AlertEvaluation
                {
                    IsInAlert = true,
                    AlertType = AlertTypeBelowTarget,
                    Message = $"Valeur mesuree ({measuredValue:0.##}) inferieure a la cible ({targetValue:0.##})."
                };
            }

            return new AlertEvaluation
            {
                IsInAlert = false,
                AlertType = null,
                Message = null
            };
        }

        public sealed class AlertEvaluation
        {
            public bool IsInAlert { get; init; }
            public string? AlertType { get; init; }
            public string? Message { get; init; }
        }
    }
}
