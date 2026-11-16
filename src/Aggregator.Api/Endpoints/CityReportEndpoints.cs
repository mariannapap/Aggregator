using Aggregator.Api.Auth;
using Aggregator.Api.CityReports;
using Aggregator.Api.Contracts;
using Aggregator.Core.CityReports;
using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Aggregator.Api.Endpoints;

internal static class CityReportEndpoints
{
    public static IEndpointRouteBuilder MapCityReports(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/city-report")
            .RequireAuthorization(AuthOptions.ReaderPolicy)
            .WithTags("City report")
            .MapGet("/{country}/{city}", GetCityReport)
            .WithName("GetCityReport")
            .WithSummary("Aggregates geocoding, current weather, air quality, marine and hourly data for every matching place.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return endpoints;
    }

    private static async Task<Results<Ok<CityReportResponse>, ValidationProblem, ProblemHttpResult>> GetCityReport(
        [AsParameters] CityReportQuery query,
        CityReportQueryParser parser,
        CityReportService service,
        CancellationToken cancellationToken)
    {
        var parsed = parser.Parse(query);
        if (!parsed.IsValid)
            return TypedResults.ValidationProblem(parsed.Errors);

        var request = parsed.Request;
        return await service.GetAsync(request, cancellationToken) switch
        {
            CityReportOutcome.Found found => TypedResults.Ok(found.Report.ToResponse()),
            CityReportOutcome.GeocodingUnavailable => TypedResults.Problem(
                title: "Geocoding is unavailable",
                detail: "The city could not be resolved to coordinates and no cached result exists.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                extensions: new Dictionary<string, object?>
                {
                    ["sources"] = new Dictionary<string, string> { [SourceNames.Geocoding] = SourceStatus.Unavailable.ToContract() }
                }),
            _ => TypedResults.Problem(
                title: "City not found",
                detail: $"No city or airport named '{request.City}' was found in {request.Country}.",
                statusCode: StatusCodes.Status404NotFound)
        };
    }
}
