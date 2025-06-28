using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DatabaseAutomationPlatform.Api.Services;

/// <summary>
/// Background service that periodically runs health checks and logs results
/// </summary>
public class HealthCheckBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthCheckBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public HealthCheckBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<HealthCheckBackgroundService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Health check background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();

                var result = await healthCheckService.CheckHealthAsync(stoppingToken);

                if (result.Status == HealthStatus.Healthy)
                {
                    _logger.LogInformation("Health check passed. Status: {Status}", result.Status);
                }
                else
                {
                    _logger.LogWarning("Health check failed. Status: {Status}", result.Status);
                    
                    foreach (var entry in result.Entries)
                    {
                        _logger.LogWarning(
                            "Health check {Name} status: {Status}. Description: {Description}",
                            entry.Key,
                            entry.Value.Status,
                            entry.Value.Description ?? "N/A");

                        if (entry.Value.Exception != null)
                        {
                            _logger.LogError(entry.Value.Exception, 
                                "Health check {Name} threw exception", entry.Key);
                        }
                    }
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running health checks");
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        _logger.LogInformation("Health check background service stopped");
    }
}