using Aggregator.Core.Model;

namespace Aggregator.Core.Query;

// Relevance rank of a place for the searched name; lower is better. Ties keep the geocoder's order (stable sort).
internal static class PlaceRanker
{
    private const int TypesPerNameMatch = 10;

    public static int Rank(Place place, string searchedName)
    {
        var name = RegionMatcher.Normalize(place.Name);
        var searched = RegionMatcher.Normalize(searchedName);
        var nameMatch = name == searched ? 0
            : name.StartsWith(searched, StringComparison.Ordinal) ? 1
            : name.Contains(searched, StringComparison.Ordinal) ? 2
            : 3;

        return nameMatch * TypesPerNameMatch + TypeRank(place.FeatureCode);
    }

    // Capital, then administrative seats by level, then other settlements, then airports.
    private static int TypeRank(string featureCode) => featureCode.ToUpperInvariant() switch
    {
        "PPLC" => 0,
        "PPLA" => 1,
        "PPLA2" => 2,
        "PPLA3" => 3,
        "PPLA4" => 4,
        "PPL" => 5,
        "AIRP" => 6,
        _ => 7
    };
}
