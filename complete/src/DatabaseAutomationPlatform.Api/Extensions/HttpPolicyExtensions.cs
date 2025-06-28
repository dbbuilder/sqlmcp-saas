using Polly;
using Polly.Extensions.Http;

namespace DatabaseAutomationPlatform.Api.Extensions;

/// <summary>
/// Extension methods for configuring HTTP resilience policies
/// </summary>
public static class HttpPolicyExtensions
{
    /// <summary>
    /// Gets the retry policy for HTTP requests
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.Values.ContainsKey("logger") ? context.Values["logger"] as ILogger : null;
                    logger?.LogWarning("Retry {RetryCount} after {TimeSpan}ms", retryCount, timespan.TotalMilliseconds);
                });
    }

    /// <summary>
    /// Gets the circuit breaker policy for HTTP requests
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                5,
                TimeSpan.FromSeconds(30),
                onBreak: (result, timespan) =>
                {
                    // Log circuit breaker opening
                },
                onReset: () =>
                {
                    // Log circuit breaker closing
                });
    }

    /// <summary>
    /// Gets the timeout policy for HTTP requests
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(int seconds = 30)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(seconds);
    }

    /// <summary>
    /// Gets a combined policy with retry, circuit breaker, and timeout
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy()
    {
        var retryPolicy = GetRetryPolicy();
        var circuitBreakerPolicy = GetCircuitBreakerPolicy();
        var timeoutPolicy = GetTimeoutPolicy();

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy, timeoutPolicy);
    }
}