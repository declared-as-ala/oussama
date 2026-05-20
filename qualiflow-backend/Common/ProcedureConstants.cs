using System.Collections.Generic;

namespace DocApi.Common
{
    public static class ProcedureConstants
    {
        public const string StatusActif = "ACTIF";
        public const string StatusInactif = "INACTIF";

        public static readonly HashSet<string> AllowedStatuses = new()
        {
            StatusActif,
            StatusInactif
        };
    }
}
