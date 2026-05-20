using System;
using System.Collections.Generic;

namespace DocApi.Common
{
    public static class CorrectiveActionConstants
    {
        public const string TypeCurative = "CURATIVE";
        public const string TypeCorrective = "CORRECTIVE";
        public const string TypeRisk = "RISQUE";

        public const string StatusPlanned = "PLANIFIEE";
        public const string StatusInProgress = "EN_COURS";
        public const string StatusCompleted = "REALISEE";
        public const string StatusVerified = "VERIFIEE";

        // Legacy status aliases still accepted from older clients.
        public const string LegacyStatusTodo = "A_FAIRE";
        public const string LegacyStatusDone = "TERMINEE";
        public const string LegacyStatusOverdue = "EN_RETARD";

        public static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            TypeCurative,
            TypeCorrective,
            TypeRisk
        };

        public static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            StatusPlanned,
            StatusInProgress,
            StatusCompleted,
            StatusVerified
        };

        private static readonly Dictionary<string, string> LegacyStatusMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [LegacyStatusTodo] = StatusPlanned,
            [LegacyStatusDone] = StatusCompleted,
            [LegacyStatusOverdue] = StatusInProgress
        };

        private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
        {
            [StatusPlanned] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { StatusInProgress, StatusCompleted },
            [StatusInProgress] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { StatusPlanned, StatusCompleted },
            [StatusCompleted] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { StatusInProgress, StatusVerified },
            [StatusVerified] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { StatusInProgress, StatusCompleted }
        };

        public static string? NormalizeType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim().ToUpperInvariant();
        }

        public static string? NormalizeStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim().ToUpperInvariant();
            return LegacyStatusMap.TryGetValue(normalized, out var mapped)
                ? mapped
                : normalized;
        }

        public static bool IsCompletedStatus(string? status)
        {
            var normalized = NormalizeStatus(status);
            return string.Equals(normalized, StatusCompleted, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, StatusVerified, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAllowedTransition(string currentStatus, string nextStatus)
        {
            var current = NormalizeStatus(currentStatus);
            var next = NormalizeStatus(nextStatus);

            if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(next))
            {
                return false;
            }

            if (string.Equals(current, next, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return AllowedTransitions.TryGetValue(current, out var allowedTargets)
                && allowedTargets.Contains(next);
        }
    }
}
