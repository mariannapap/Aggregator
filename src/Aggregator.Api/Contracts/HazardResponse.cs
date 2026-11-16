namespace Aggregator.Api.Contracts;

public sealed record HazardResponse(string Title, string Category, DateTimeOffset Date, double DistanceKm);
