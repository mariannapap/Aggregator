namespace Aggregator.Api.Contracts;

public sealed record CityReportResponse(FiltersResponse Filters, bool Degraded, IReadOnlyList<PlaceResponse> Places);
