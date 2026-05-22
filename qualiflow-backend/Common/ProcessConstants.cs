using System.Collections.Generic;

namespace DocApi.Common
{
    public static class ProcessConstants
    {
        public const string TypePilotage = "PILOTAGE";
        public const string TypeRealisation = "REALISATION";
        public const string TypeSupport = "SUPPORT";

        public const string StatusActif = "ACTIF";
        public const string StatusInactif = "INACTIF";

        public const string ActorPilote = "PILOTE";
        public const string ActorPiloteProcedure = "PILOTE_PROCEDURE";
        public const string ActorCopilote = "COPILOTE";
        public const string ActorContributeur = "CONTRIBUTEUR";
        public const string ActorObservateur = "OBSERVATEUR";

        public static readonly HashSet<string> AllowedTypes = new()
        {
            TypePilotage,
            TypeRealisation,
            TypeSupport
        };

        public static readonly HashSet<string> AllowedStatuses = new()
        {
            StatusActif,
            StatusInactif
        };

        public static readonly HashSet<string> AllowedActorTypes = new()
        {
            ActorPilote,
            ActorPiloteProcedure,
            ActorCopilote,
            ActorContributeur,
            ActorObservateur
        };
    }
}
