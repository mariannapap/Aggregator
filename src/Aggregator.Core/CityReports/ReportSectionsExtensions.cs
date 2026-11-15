using Aggregator.Core.Model;

namespace Aggregator.Core.CityReports;

public static class ReportSectionsExtensions
{
    public const ReportSections DataSections = ReportSections.Weather | ReportSections.AirQuality | ReportSections.Marine;

    public static ReportSections ToSection(this HourlyCategory category) => category switch
    {
        HourlyCategory.Weather => ReportSections.Weather,
        HourlyCategory.AirQuality => ReportSections.AirQuality,
        HourlyCategory.Marine => ReportSections.Marine,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    public static bool IncludesHourlyFor(this ReportSections sections, HourlyCategory category) =>
        (sections & DataSections) == ReportSections.None || sections.HasFlag(category.ToSection());
}
