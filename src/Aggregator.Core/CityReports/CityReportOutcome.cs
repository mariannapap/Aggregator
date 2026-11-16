using Aggregator.Core.Model;

namespace Aggregator.Core.CityReports;

public abstract record CityReportOutcome
{
    private CityReportOutcome()
    {
    }

    public sealed record Found(CityReport Report) : CityReportOutcome;

    public sealed record CityNotFound : CityReportOutcome;

    public sealed record GeocodingUnavailable : CityReportOutcome;

    public sealed record HourlyRangeOutsideWindow(HourlyWindow Window) : CityReportOutcome;
}
