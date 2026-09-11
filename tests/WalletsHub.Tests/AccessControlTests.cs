using WalletsHub.Api;
using Xunit;

namespace WalletsHub.Tests;

public sealed class AccessControlTests
{
    [Theory]
    [InlineData(Roles.Owner, Roles.Owner, true)]
    [InlineData(Roles.Owner, Roles.Admin, true)]
    [InlineData(Roles.Admin, Roles.Owner, false)]
    [InlineData(Roles.Admin, Roles.Admin, false)]
    [InlineData(Roles.Admin, Roles.Manager, true)]
    [InlineData(Roles.Manager, Roles.Admin, false)]
    [InlineData(Roles.Manager, Roles.Employee, true)]
    [InlineData(Roles.Employee, Roles.Employee, false)]
    public void RoleHierarchyPreventsPrivilegeEscalation(string actor, string target, bool expected) =>
        Assert.Equal(expected, AccessControl.CanManageRole(actor, target));
}
