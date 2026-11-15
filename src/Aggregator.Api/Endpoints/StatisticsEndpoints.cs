using Aggregator.Api.Auth;
using Aggregator.Api.Contracts;
using Aggregator.Core.Statistics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Aggregator.Api.Endpoints;

internal static class StatisticsEndpoints
{
    public static IEndpointRouteBuilder MapStatistics(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/stats", GetStatistics)
            .RequireAuthorization(AuthOptions.ReaderPolicy)
            .WithTags("Statistics")
            .WithSummary("Total requests and average response time per external API, grouped in performance buckets.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        return endpoints;
    }

    private static Ok<StatisticsResponse> GetStatistics(StatisticsStore store, IOptions<StatisticsOptions> options) =>
        TypedResults.Ok(store.Snapshot().ToResponse(options.Value));
}
