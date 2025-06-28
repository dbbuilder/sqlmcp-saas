using Microsoft.AspNetCore.Authorization;

namespace DatabaseAutomationPlatform.Api.Authorization;

/// <summary>
/// Requirement for permission-based authorization
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }
}

/// <summary>
/// Handler for permission-based authorization
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, 
        PermissionRequirement requirement)
    {
        // Check if user has the required permission
        var hasPermission = context.User.Claims
            .Any(c => c.Type == "permission" && c.Value == requirement.Permission);

        if (hasPermission)
        {
            _logger.LogInformation("User {User} granted permission {Permission}",
                context.User.Identity?.Name, requirement.Permission);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning("User {User} denied permission {Permission}",
                context.User.Identity?.Name, requirement.Permission);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Policy provider for dynamic permission-based policies
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;
    private const string PermissionPrefix = "Permission:";

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName.Substring(PermissionPrefix.Length);
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        // Fallback to default policy provider
        return _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallbackPolicyProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackPolicyProvider.GetFallbackPolicyAsync();
    }
}

/// <summary>
/// Attribute for permission-based authorization
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        Policy = $"{PermissionPolicyProvider.PermissionPrefix}{permission}";
    }
}

/// <summary>
/// Constants for system permissions
/// </summary>
public static class Permissions
{
    public const string QueryExecute = "query:execute";
    public const string QueryOptimize = "query:optimize";
    public const string CommandExecute = "command:execute";
    public const string SchemaRead = "schema:read";
    public const string SchemaModify = "schema:modify";
    public const string AnalyzePerformance = "analyze:performance";
    public const string AnalyzeData = "analyze:data";
    public const string BackupCreate = "backup:create";
    public const string BackupRestore = "backup:restore";
    public const string MaintenanceRun = "maintenance:run";
    public const string SecurityAudit = "security:audit";
    public const string AdminFull = "admin:full";
}