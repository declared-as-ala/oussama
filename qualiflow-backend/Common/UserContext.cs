namespace DocApi.Common
{
    public sealed class UserContext
    {
        public int UserId { get; init; }
        public string Role { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public int? OrganizationId { get; init; }

        public bool IsSuperAdmin => Role == UserRoles.SUPER_ADMIN;

        public bool CanWriteProcesses => Role == UserRoles.SUPER_ADMIN
            || Role == UserRoles.ADMIN_ORG
            || Role == UserRoles.RESPONSABLE_QUALITE
            || Role == UserRoles.UTILISATEUR;

        public bool CanReadProcesses => CanWriteProcesses
            || Role == UserRoles.UTILISATEUR;

        public bool CanWriteProcedures => Role == UserRoles.ADMIN_ORG
            || Role == UserRoles.RESPONSABLE_QUALITE
            || Role == UserRoles.UTILISATEUR;

        public bool CanReadProcedures => Role == UserRoles.SUPER_ADMIN
            || Role == UserRoles.ADMIN_ORG
            || Role == UserRoles.RESPONSABLE_QUALITE
            || Role == UserRoles.UTILISATEUR;

        public bool CanWriteNonConformities => Role == UserRoles.ADMIN_ORG
            || Role == UserRoles.RESPONSABLE_QUALITE;

        public bool CanCreateNonConformities => CanWriteNonConformities
            || Role == UserRoles.UTILISATEUR;

        public bool CanReadNonConformities => Role == UserRoles.ADMIN_ORG
            || Role == UserRoles.RESPONSABLE_QUALITE
            || Role == UserRoles.UTILISATEUR;

        public bool CanWriteCorrectiveActions => Role == UserRoles.ADMIN_ORG
            || Role == UserRoles.RESPONSABLE_QUALITE;

        public bool CanReadCorrectiveActions => CanWriteCorrectiveActions
            || Role == UserRoles.UTILISATEUR;

        public bool CanWriteIndicators => Role == UserRoles.ADMIN_ORG
            || Role == UserRoles.RESPONSABLE_QUALITE;

        public bool CanReadIndicators => CanWriteIndicators
            || Role == UserRoles.UTILISATEUR;

        public bool CanWriteDocuments => Role == UserRoles.ADMIN_ORG
            || Role == UserRoles.RESPONSABLE_QUALITE
            || Role == UserRoles.UTILISATEUR;

        public bool CanSubmitDocuments => CanWriteDocuments
            || Role == UserRoles.UTILISATEUR;

        public bool CanReadDocuments => Role == UserRoles.ADMIN_ORG
            || Role == UserRoles.RESPONSABLE_QUALITE
            || Role == UserRoles.UTILISATEUR;


        public bool CanReadNotifications => Role == UserRoles.ADMIN_ORG
            || Role == UserRoles.SUPER_ADMIN
            || Role == UserRoles.RESPONSABLE_QUALITE
            || Role == UserRoles.UTILISATEUR;

        public bool CanManageAlertRules => Role == UserRoles.ADMIN_ORG
            || Role == UserRoles.RESPONSABLE_QUALITE;
    }
}
