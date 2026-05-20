using System.Collections.Generic;

namespace DocApi.Common
{
    public static class NonConformityConstants
    {
        public const string TypeInterne = "INTERNE";
        public const string TypeExterne = "EXTERNE";

        public const string SeverityMineure = "MINEURE";
        public const string SeverityMajeure = "MAJEURE";
        public const string SeverityCritique = "CRITIQUE";

        public const string StatusOuverte = "OUVERTE";
        public const string StatusEnAttenteValidation = "EN_ATTENTE_VALIDATION";
        public const string StatusEnCours = "EN_COURS";
        public const string StatusCloturee = "CLOTUREE";

        public const string ActionStatusAFaire = "A_FAIRE";
        public const string ActionStatusEnCours = "EN_COURS";
        public const string ActionStatusTerminee = "TERMINEE";
        public const string ActionStatusEnRetard = "EN_RETARD";

        public static readonly HashSet<string> AllowedTypes = new()
        {
            TypeInterne,
            TypeExterne
        };

        public static readonly HashSet<string> AllowedSeverities = new()
        {
            SeverityMineure,
            SeverityMajeure,
            SeverityCritique
        };

        public static readonly HashSet<string> AllowedStatuses = new()
        {
            StatusEnAttenteValidation,
            StatusOuverte,
            StatusEnCours,
            StatusCloturee
        };

        public static readonly HashSet<string> AllowedActionStatuses = new()
        {
            ActionStatusAFaire,
            ActionStatusEnCours,
            ActionStatusTerminee,
            ActionStatusEnRetard
        };
    }
}
