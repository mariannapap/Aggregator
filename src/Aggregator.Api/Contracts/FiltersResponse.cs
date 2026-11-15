namespace Aggregator.Api.Contracts;

public sealed record FiltersResponse(RegionFilterResponse? Region, HourlyRangeResponse? Hourly);
