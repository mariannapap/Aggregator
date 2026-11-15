namespace Aggregator.Core.CityReports;

public sealed record PlaceSort(PlaceSortField Field, bool Descending)
{
    public static PlaceSort Default { get; } = new(PlaceSortField.Relevance, false);
}
