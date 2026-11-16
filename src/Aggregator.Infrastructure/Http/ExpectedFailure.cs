using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace Aggregator.Infrastructure.Http;

internal static class ExpectedFailure
{
    // Rejections by our own resilience pipeline, network errors and HttpClient's timeout: the type and message say all there
    // is to say, and an open circuit can produce one per call. Anything else (e.g. a malformed body) keeps its stack trace.
    public static string? Describe(Exception exception) => exception switch
    {
        BrokenCircuitException or RateLimiterRejectedException or TimeoutRejectedException or HttpRequestException or TaskCanceledException
            => $"{exception.GetType().Name}: {exception.Message}",
        _ => null
    };
}
