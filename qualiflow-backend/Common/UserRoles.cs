namespace DocApi.Common
{
    public static class UserRoles
    {
        public const string SUPER_ADMIN = "SUPER_ADMIN";
        public const string ADMIN_ORG = "ADMIN_ORG";
        public const string RESPONSABLE_QUALITE = "RESPONSABLE_QUALITE";
        public const string CHEF_SERVICE = "CHEF_SERVICE";
        public const string UTILISATEUR = "UTILISATEUR";

        public static readonly string[] AllRoles = 
        {
            SUPER_ADMIN,
            ADMIN_ORG,
            RESPONSABLE_QUALITE,
            CHEF_SERVICE,
            UTILISATEUR
        };
    }
}
