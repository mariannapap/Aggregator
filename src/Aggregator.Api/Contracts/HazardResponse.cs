namespace Aggregator.Api.Contracts;

/// <summary>An open natural event near the place, from NASA EONET.</summary>
/// <param name="Title">The event's title, e.g. a wildfire's name.</param>
/// <param name="Category">The EONET category title, e.g. Wildfires or Severe Storms; Other when the event has none.</param>
/// <param name="Date">When the event's latest reported position was observed.</param>
/// <param name="DistanceKm">Distance from the place to that position, in km, rounded to 0.1. For an area, the position is the average of its outline's points.</param>
public sealed record HazardResponse(string Title, string Category, DateTimeOffset Date, double DistanceKm);
