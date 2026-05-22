using System.Collections.Generic;

namespace DocApi.Common
{
    public static class NotificationConstants
    {
        public const string CategoryInfo = "INFO";
        public const string CategorySuccess = "SUCCESS";
        public const string CategoryWarning = "WARNING";
        public const string CategoryError = "ERROR";

        public const string PriorityLow = "LOW";
        public const string PriorityMedium = "MEDIUM";
        public const string PriorityHigh = "HIGH";
        public const string PriorityCritical = "CRITICAL";

        public const string TypeDocumentApprovalRequired = "DOCUMENT_APPROVAL_REQUIRED";
        public const string TypeDocumentExpired = "DOCUMENT_EXPIRED";
        public const string TypeDocumentNewVersion = "DOCUMENT_NEW_VERSION";
        public const string TypeDocumentCreatedWorkflow = "DocumentCreated";
        public const string TypeDocumentSubmittedWorkflow = "DocumentSubmitted";
        public const string TypeDocumentApprovedWorkflow = "DocumentApproved";
        public const string TypeDocumentRejectedWorkflow = "DocumentRejected";
        public const string TypeDocumentArchivedWorkflow = "DocumentArchived";
        public const string TypeDocumentExpiring30Workflow = "DocumentExpiring30";
        public const string TypeDocumentExpiring7Workflow = "DocumentExpiring7";
        public const string TypeDocumentExpiring1Workflow = "DocumentExpiring1";
        public const string TypeDocumentExpiredWorkflow = "DocumentExpired";
        public const string TypeProcessWithoutPilot = "PROCESS_WITHOUT_PILOT";
        public const string TypeProcedureWithoutResponsible = "PROCEDURE_WITHOUT_RESPONSIBLE";
        public const string TypeNonConformityCreated = "NONCONFORMITY_CREATED";
        public const string TypeNonConformityCritical = "NONCONFORMITY_CRITICAL";
        public const string TypeCorrectiveActionAssigned = "CORRECTIVE_ACTION_ASSIGNED";
        public const string TypeCorrectiveActionDueSoon = "CORRECTIVE_ACTION_DUE_SOON";
        public const string TypeCorrectiveActionOverdue = "CORRECTIVE_ACTION_OVERDUE";
        public const string TypeIndicatorAlert = "INDICATOR_ALERT";
        public const string TypeUserCreated = "USER_CREATED";
        public const string TypeUserDisabled = "USER_DISABLED";
        public const string TypeOrganizationSuspended = "ORGANIZATION_SUSPENDED";
        public const string TypeOrganizationSubscriptionExpired = "ORGANIZATION_SUBSCRIPTION_EXPIRED";
        public const string TypeSystemAlert = "SYSTEM_ALERT";

        public static readonly HashSet<string> AllowedCategories = new()
        {
            CategoryInfo,
            CategorySuccess,
            CategoryWarning,
            CategoryError
        };

        public static readonly HashSet<string> AllowedPriorities = new()
        {
            PriorityLow,
            PriorityMedium,
            PriorityHigh,
            PriorityCritical
        };

        public static readonly HashSet<string> AllowedTypes = new()
        {
            TypeDocumentApprovalRequired,
            TypeDocumentExpired,
            TypeDocumentNewVersion,
            TypeDocumentCreatedWorkflow,
            TypeDocumentSubmittedWorkflow,
            TypeDocumentApprovedWorkflow,
            TypeDocumentRejectedWorkflow,
            TypeDocumentArchivedWorkflow,
            TypeDocumentExpiring30Workflow,
            TypeDocumentExpiring7Workflow,
            TypeDocumentExpiring1Workflow,
            TypeDocumentExpiredWorkflow,
            TypeProcessWithoutPilot,
            TypeProcedureWithoutResponsible,
            TypeNonConformityCreated,
            TypeNonConformityCritical,
            TypeCorrectiveActionAssigned,
            TypeCorrectiveActionDueSoon,
            TypeCorrectiveActionOverdue,
            TypeIndicatorAlert,
            TypeUserCreated,
            TypeUserDisabled,
            TypeOrganizationSuspended,
            TypeOrganizationSubscriptionExpired,
            TypeSystemAlert
        };

        public static readonly HashSet<string> AllowedNotificationRoles = new()
        {
            UserRoles.SUPER_ADMIN,
            UserRoles.ADMIN_ORG,
            UserRoles.RESPONSABLE_QUALITE,
            UserRoles.UTILISATEUR
        };
    }
}
