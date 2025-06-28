using System.Diagnostics;

namespace DatabaseAutomationPlatform.Api.Services;

/// <summary>
/// Background service that collects and reports application metrics
/// </summary>
public class MetricsCollectionService : BackgroundService
{
    private readonly ILogger<MetricsCollectionService> _logger;
    private readonly TimeSpan _collectionInterval = TimeSpan.FromSeconds(30);
    private readonly Process _currentProcess = Process.GetCurrentProcess();

    public MetricsCollectionService(ILogger<MetricsCollectionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metrics collection service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                CollectMetrics();
                await Task.Delay(_collectionInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting metrics");
                await Task.Delay(_collectionInterval, stoppingToken);
            }
        }

        _logger.LogInformation("Metrics collection service stopped");
    }

    private void CollectMetrics()
    {
        try
        {
            _currentProcess.Refresh();

            var metrics = new
            {
                Timestamp = DateTimeOffset.UtcNow,
                Process = new
                {
                    WorkingSetMB = _currentProcess.WorkingSet64 / (1024 * 1024),
                    PrivateMemoryMB = _currentProcess.PrivateMemorySize64 / (1024 * 1024),
                    ThreadCount = _currentProcess.Threads.Count,
                    HandleCount = _currentProcess.HandleCount,
                    TotalProcessorTime = _currentProcess.TotalProcessorTime.TotalSeconds
                },
                GC = new
                {
                    TotalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024),
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2)
                }
            };

            _logger.LogInformation("Application metrics: {@Metrics}", metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect process metrics");
        }
    }

    public override void Dispose()
    {
        _currentProcess?.Dispose();
        base.Dispose();
    }
}