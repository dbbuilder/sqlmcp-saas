using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DatabaseAutomationPlatform.Infrastructure.Security;

namespace DatabaseAutomationPlatform.Infrastructure.HealthChecks
{
    /// <summary>
    /// Health check for Azure Key Vault connectivity and functionality
    /// </summary>
    public class AzureKeyVaultHealthCheck : IHealthCheck
    {
        private readonly ISecretManager _secretManager;
        private readonly ILogger<AzureKeyVaultHealthCheck> _logger;
        private const string HealthCheckSecretName = "health-check-probe";
        private const int TimeoutMilliseconds = 5000;

        public AzureKeyVaultHealthCheck(
            ISecretManager secretManager,
            ILogger<AzureKeyVaultHealthCheck> logger)
        {
            _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeoutMilliseconds);

                // Check if we can read from Key Vault
                var exists = await _secretManager.SecretExistsAsync(HealthCheckSecretName);
                
                if (!exists)
                {
                    // Try to create the health check secret
                    _logger.LogInformation("Creating health check secret in Key Vault");
                    await _secretManager.SetSecretAsync(
                        HealthCheckSecretName, 
                        $"Health check probe created at {DateTime.UtcNow:O}");
                }
                else
                {
                    // Try to read the existing secret
                    var secretValue = await _secretManager.GetSecretAsync(HealthCheckSecretName);
                    
                    if (string.IsNullOrEmpty(secretValue))
                    {
                        return HealthCheckResult.Degraded(
                            "Key Vault returned empty value for health check secret",
                            data: new System.Collections.Generic.Dictionary<string, object>
                            {
                                ["ElapsedMilliseconds"] = stopwatch.ElapsedMilliseconds,
                                ["SecretName"] = HealthCheckSecretName
                            });
                    }
                }

                stopwatch.Stop();

                return HealthCheckResult.Healthy(
                    "Azure Key Vault is accessible and functioning properly",
                    data: new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["ElapsedMilliseconds"] = stopwatch.ElapsedMilliseconds,
                        ["SecretName"] = HealthCheckSecretName,
                        ["Operation"] = exists ? "Read" : "Create"
                    });
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                
                _logger.LogWarning(
                    "Azure Key Vault health check timed out after {TimeoutMs}ms", 
                    TimeoutMilliseconds);

                return HealthCheckResult.Unhealthy(
                    $"Azure Key Vault health check timed out after {TimeoutMilliseconds}ms",
                    data: new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["ElapsedMilliseconds"] = stopwatch.ElapsedMilliseconds,
                        ["TimeoutMilliseconds"] = TimeoutMilliseconds
                    });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                _logger.LogError(ex, "Azure Key Vault health check failed");

                return HealthCheckResult.Unhealthy(
                    "Azure Key Vault is not accessible",
                    exception: ex,
                    data: new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["ElapsedMilliseconds"] = stopwatch.ElapsedMilliseconds,
                        ["ErrorType"] = ex.GetType().Name,
                        ["ErrorMessage"] = ex.Message
                    });
            }
        }
    }
}