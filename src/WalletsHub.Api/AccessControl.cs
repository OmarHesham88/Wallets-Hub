namespace WalletsHub.Api;

public static class AccessControl
{
    public static int RoleRank(string role) => role switch
    {
        Roles.Owner => 4,
        Roles.Admin => 3,
        Roles.Manager => 2,
        Roles.Employee => 1,
        _ => 0
    };

    public static bool CanManageRole(string actorRole, string targetRole) =>
        actorRole == Roles.Owner
            ? Roles.OrganizationRoles.Contains(targetRole)
            : RoleRank(actorRole) > RoleRank(targetRole);
}
