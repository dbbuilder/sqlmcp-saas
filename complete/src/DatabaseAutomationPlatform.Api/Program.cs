using DatabaseAutomationPlatform.Api.Authorization;
using DatabaseAutomationPlatform.Api.Extensions;
using DatabaseAutomationPlatform.Api.Middleware;
using DatabaseAutomationPlatform.Infrastructure.Extensions;
using HealthChecks.UI.Client;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.FeatureManagement;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using System.Text;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.ApplicationInsights(services.GetRequiredService<TelemetryConfiguration>(), TelemetryConverter.Traces));

// Add services to the container
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new ProducesAttribute("application/json"));
    options.Filters.Add(new ConsumesAttribute("application/json"));
});

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// Add FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// Add feature management
builder.Services.AddFeatureManagement();

// Configure authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
})
.AddApiKeyAuthentication();

// Register authorization services
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IApiKeyValidationService, ApiKeyValidationService>();

builder.Services.AddAuthorization(options =>
{
    // Role-based policies
    options.AddPolicy("Developer", policy => policy.RequireClaim("role", "Developer"));
    options.AddPolicy("DBA", policy => policy.RequireClaim("role", "DBA"));
    options.AddPolicy("Admin", policy => policy.RequireClaim("role", "Admin"));
    options.AddPolicy("Viewer", policy => policy.RequireClaim("role", "Viewer"));
    
    // Combined policies
    options.AddPolicy("DeveloperOrDBA", policy => 
        policy.RequireAssertion(context =>
            context.User.HasClaim("role", "Developer") ||
            context.User.HasClaim("role", "DBA")));
    
    options.AddPolicy("AdminOnly", policy => 
        policy.RequireClaim("role", "Admin")
              .RequireAuthenticatedUser());
});

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Database Automation Platform API", 
        Version = "v1",
        Description = "MCP-compliant API for AI-assisted database operations",
        Contact = new OpenApiContact
        {
            Name = "Database Automation Team",
            Email = "support@dbautomation.com"
        }
    });
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    
    // Include XML comments
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry(new ApplicationInsightsServiceOptions
{
    EnableAdaptiveSampling = true,
    EnablePerformanceCounterCollectionModule = true,
    EnableRequestTrackingTelemetryModule = true,
    EnableDependencyTrackingTelemetryModule = true
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection not configured"),
        name: "database",
        tags: new[] { "db", "sql" })
    .AddAzureKeyVault(
        _ => { },
        name: "keyvault",
        tags: new[] { "security", "azure" });

// Register infrastructure services
builder.Services.AddInfrastructureServices(builder.Configuration);

// Register API services
builder.Services.AddApiServices(builder.Configuration);

// Add HTTP client with Polly
builder.Services.AddHttpClient("McpClient")
    .AddPolicyHandler(HttpPolicyExtensions.GetRetryPolicy())
    .AddPolicyHandler(HttpPolicyExtensions.GetCircuitBreakerPolicy());

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowSpecificOrigins");

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<SecurityHeadersMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

Log.Information("Starting Database Automation Platform API");

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}