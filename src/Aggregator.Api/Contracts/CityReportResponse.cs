namespace Aggregator.Api.Contracts;

public sealed record CityReportResponse(FiltersResponse Filters, IReadOnlyList<PlaceResponse> Places);
