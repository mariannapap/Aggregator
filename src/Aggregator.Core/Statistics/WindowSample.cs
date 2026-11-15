namespace Aggregator.Core.Statistics;

public sealed record WindowSample(long Count, double TotalMs)
{
    public double AverageMs => Count == 0 ? 0 : TotalMs / Count;
}
