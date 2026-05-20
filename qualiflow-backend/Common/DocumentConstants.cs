using System.Collections.Generic;

namespace DocApi.Common
{
    public static class DocumentConstants
    {
        public const string TypeManuel = "MANUEL";
        public const string TypeProcedure = "PROCEDURE";
        public const string TypeEnregistrement = "ENREGISTREMENT";
        public const string TypeFormulaire = "FORMULAIRE";
        public const string TypeInstruction = "INSTRUCTION";
        public const string TypePolitique = "POLITIQUE";
        public const string TypeAutre = "AUTRE";

        public const string StatusBrouillon = "BROUILLON";
        public const string StatusEnRevision = "EN_REVISION";
        public const string StatusApprouve = "APPROUVE";
        public const string StatusPublie = "PUBLIE";
        public const string StatusRejete = "REJETE";
        public const string StatusPerime = "PERIME";
        public const string StatusArchive = "ARCHIVE";

        public static readonly HashSet<string> AllowedTypes = new()
        {
            TypeManuel,
            TypeProcedure,
            TypeEnregistrement,
            TypeFormulaire,
            TypeInstruction,
            TypePolitique,
            TypeAutre
        };

        public static readonly HashSet<string> AllowedStatuses = new()
        {
            StatusBrouillon,
            StatusEnRevision,
            StatusApprouve,
            StatusPublie,
            StatusRejete,
            StatusPerime,
            StatusArchive
        };
    }
}
