namespace Aggregator.Core.CityReports;

public abstract record CityReportOutcome
{
    private CityReportOutcome()
    {
    }

    public sealed record Found(CityReport Report) : CityReportOutcome;

    public sealed record CityNotFound : CityReportOutcome;

    public sealed record GeocodingUnavailable : CityReportOutcome;
}
