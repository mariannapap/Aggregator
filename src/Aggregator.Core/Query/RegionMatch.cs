namespace Aggregator.Core.Query;

public sealed record RegionMatch(string Requested, IReadOnlyList<string> Matched)
{
    public bool Applied => Matched.Count > 0;
}
