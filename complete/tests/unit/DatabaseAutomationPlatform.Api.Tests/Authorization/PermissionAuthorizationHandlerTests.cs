using DatabaseAutomationPlatform.Api.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace DatabaseAutomationPlatform.Api.Tests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private readonly Mock<ILogger<PermissionAuthorizationHandler>> _loggerMock;
    private readonly PermissionAuthorizationHandler _sut;

    public PermissionAuthorizationHandlerTests()
    {
        _loggerMock = new Mock<ILogger<PermissionAuthorizationHandler>>();
        _sut = new PermissionAuthorizationHandler(_loggerMock.Object);
    }

    [Fact]
    public async Task HandleRequirementAsync_UserHasPermission_Succeeds()
    {
        // Arrange
        var requirement = new PermissionRequirement("query:execute");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("permission", "query:execute"),
            new Claim("permission", "schema:read")
        }));
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            null);

        // Act
        await _sut.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_UserLacksPermission_Fails()
    {
        // Arrange
        var requirement = new PermissionRequirement("command:execute");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("permission", "query:execute"),
            new Claim("permission", "schema:read")
        }));
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            null);

        // Act
        await _sut.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_UserHasNoPermissions_Fails()
    {
        // Arrange
        var requirement = new PermissionRequirement("query:execute");
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            null);

        // Act
        await _sut.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_MultiplePermissions_ChecksCorrectly()
    {
        // Arrange
        var requirement = new PermissionRequirement("admin:full");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("permission", "query:execute"),
            new Claim("permission", "command:execute"),
            new Claim("permission", "schema:read"),
            new Claim("permission", "admin:full")
        }));
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            null);

        // Act
        await _sut.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }
}

public class PermissionPolicyProviderTests
{
    private readonly PermissionPolicyProvider _sut;

    public PermissionPolicyProviderTests()
    {
        var options = Options.Create(new AuthorizationOptions());
        _sut = new PermissionPolicyProvider(options);
    }

    [Fact]
    public async Task GetPolicyAsync_PermissionPolicy_ReturnsCorrectPolicy()
    {
        // Arrange
        var policyName = "Permission:query:execute";

        // Act
        var policy = await _sut.GetPolicyAsync(policyName);

        // Assert
        policy.Should().NotBeNull();
        policy!.Requirements.Should().ContainSingle();
        policy.Requirements.First().Should().BeOfType<PermissionRequirement>();
        var requirement = policy.Requirements.First() as PermissionRequirement;
        requirement!.Permission.Should().Be("query:execute");
    }

    [Fact]
    public async Task GetPolicyAsync_NonPermissionPolicy_ReturnsFallback()
    {
        // Arrange
        var policyName = "Developer";

        // Act
        var policy = await _sut.GetPolicyAsync(policyName);

        // Assert
        // Should return null as the fallback provider doesn't have this policy
        policy.Should().BeNull();
    }

    [Fact]
    public async Task GetDefaultPolicyAsync_ReturnsFallbackDefault()
    {
        // Act
        var policy = await _sut.GetDefaultPolicyAsync();

        // Assert
        policy.Should().NotBeNull();
        policy.Requirements.Should().ContainSingle(r => r is DenyAnonymousAuthorizationRequirement);
    }
}