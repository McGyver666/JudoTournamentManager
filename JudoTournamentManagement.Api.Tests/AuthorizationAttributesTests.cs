using System.Reflection;
using JudoTournamentManagement.Api.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace JudoTournamentManagement.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class AuthorizationAttributesTests
{
    [Theory]
    [InlineData(typeof(TournamentsController), nameof(TournamentsController.CreateAsync), "Admin,Operator")]
    [InlineData(typeof(TournamentsController), nameof(TournamentsController.UpdateAsync), "Admin,Operator")]
    [InlineData(typeof(TournamentsController), nameof(TournamentsController.DeleteAsync), "Admin,Operator")]
    [InlineData(typeof(TatamisController), nameof(TatamisController.CreateAsync), "Admin,Operator")]
    [InlineData(typeof(TatamisController), nameof(TatamisController.UpdateAsync), "Admin,Operator")]
    [InlineData(typeof(TatamisController), nameof(TatamisController.DeleteAsync), "Admin,Operator")]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.CreateAsync), "Admin,Operator")]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.UpdateAsync), "Admin,Operator")]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.DeleteAsync), "Admin,Operator")]
    [InlineData(typeof(ClubsController), nameof(ClubsController.CreateAsync), "Admin,Operator")]
    [InlineData(typeof(ClubsController), nameof(ClubsController.UpdateAsync), "Admin,Operator")]
    [InlineData(typeof(ClubsController), nameof(ClubsController.DeleteAsync), "Admin,Operator")]
    [InlineData(typeof(AthletesController), nameof(AthletesController.CreateAsync), "Admin,Operator")]
    [InlineData(typeof(AthletesController), nameof(AthletesController.ImportAsync), "Admin,Operator")]
    [InlineData(typeof(AthletesController), nameof(AthletesController.UpdateAsync), "Admin,Operator")]
    [InlineData(typeof(AthletesController), nameof(AthletesController.DeleteAsync), "Admin,Operator")]
    [InlineData(typeof(RegistrationsController), nameof(RegistrationsController.CreateAsync), "Admin,Operator")]
    [InlineData(typeof(RegistrationsController), nameof(RegistrationsController.DeleteAsync), "Admin,Operator")]
    [InlineData(typeof(RegistrationsController), nameof(RegistrationsController.AutoAssignAsync), "Admin,Operator")]
    [InlineData(typeof(RegistrationsController), nameof(RegistrationsController.AssignCategoryAsync), "Admin,Operator")]
    [InlineData(typeof(FightsController), nameof(FightsController.GenerateDrawAsync), "Admin,Operator")]
    [InlineData(typeof(FightsController), nameof(FightsController.SwapAthletesAsync), "Admin,Operator")]
    public void Endpoint_HasExpectedAuthorizeRoles(Type controllerType, string methodName, string expectedRoles)
    {
        var method = FindMethod(controllerType, methodName);
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(expectedRoles, authorize!.Roles);
    }

    [Fact]
    public void MatchController_HasClassLevelAdminOperatorAuthorization()
    {
        var authorize = typeof(MatchController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Admin,Operator", authorize!.Roles);
    }

    [Theory]
    [InlineData(nameof(MatchController.AssignTatamiAsync))]
    [InlineData(nameof(MatchController.CorrectResultAsync))]
    public void MatchController_AdminOnlyEndpoints_AreExplicitlyRestricted(string methodName)
    {
        var method = FindMethod(typeof(MatchController), methodName);
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Admin", authorize!.Roles);
    }

    [Fact]
    public void AuditLogController_HasClassLevelAdminAuthorization()
    {
        var authorize = typeof(AuditLogController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Admin", authorize!.Roles);
    }

    private static MethodInfo FindMethod(Type controllerType, string methodName)
    {
        var method = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(m => string.Equals(m.Name, methodName, StringComparison.Ordinal));

        return method ?? throw new InvalidOperationException(
            $"Method '{methodName}' was not found on controller '{controllerType.Name}'.");
    }
}
