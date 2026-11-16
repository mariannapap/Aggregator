namespace Aggregator.Api.Contracts;

/// <summary>Every place in the country that matches the city name, with data from the requested sections.</summary>
/// <param name="Filters">The filters and paging that were applied.</param>
/// <param name="Degraded">True when any source of any returned place is partial, stale or unavailable; no_data does not count.</param>
/// <param name="Places">The places on this page, in the requested order.</param>
public sealed record CityReportResponse(FiltersResponse Filters, bool Degraded, IReadOnlyList<PlaceResponse> Places);
