namespace Aggregator.Api.Contracts;

/// <summary>The filters and paging that shaped the list of places.</summary>
/// <param name="Region">The region filter; absent when none was sent.</param>
/// <param name="Hourly">The hourly range; absent when hourly rows were not requested.</param>
/// <param name="TotalPlaces">Places that matched, after the region filter and before limit and offset.</param>
/// <param name="Truncated">True when limit or offset left out some of the matching places.</param>
public sealed record FiltersResponse(RegionFilterResponse? Region, HourlyRangeResponse? Hourly, int TotalPlaces, bool Truncated);
