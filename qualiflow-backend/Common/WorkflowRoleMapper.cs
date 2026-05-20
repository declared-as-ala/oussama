using DocApi.Domain.Enums;

namespace DocApi.Common
{
    public static class WorkflowRoleMapper
    {
        public static string ToLegacyRole(RoleType roleType)
        {
            return roleType switch
            {
                RoleType.Employee => UserRoles.UTILISATEUR,
                RoleType.DepartmentManager => UserRoles.CHEF_SERVICE,
                RoleType.QualityManager => UserRoles.RESPONSABLE_QUALITE,
                RoleType.SuperAdmin => UserRoles.SUPER_ADMIN,
                _ => UserRoles.UTILISATEUR
            };
        }

        public static RoleType ToRoleType(string legacyRole)
        {
            return legacyRole.Trim().ToUpperInvariant() switch
            {
                UserRoles.UTILISATEUR => RoleType.Employee,
                UserRoles.CHEF_SERVICE => RoleType.DepartmentManager,
                UserRoles.RESPONSABLE_QUALITE => RoleType.QualityManager,
                UserRoles.SUPER_ADMIN => RoleType.SuperAdmin,
                _ => RoleType.Employee
            };
        }
    }
}
