namespace Aggregator.Api.Contracts;

/// <summary>A position in decimal degrees, as returned by the geocoder.</summary>
/// <param name="Latitude">Latitude in decimal degrees, -90 to 90.</param>
/// <param name="Longitude">Longitude in decimal degrees, -180 to 180.</param>
public sealed record CoordinatesResponse(double Latitude, double Longitude);
