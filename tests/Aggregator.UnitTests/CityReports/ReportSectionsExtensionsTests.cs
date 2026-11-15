using Aggregator.Core.CityReports;
using Aggregator.Core.Model;

namespace Aggregator.UnitTests.CityReports;

public sealed class ReportSectionsExtensionsTests
{
    [Theory]
    [InlineData(HourlyCategory.Weather, ReportSections.Weather)]
    [InlineData(HourlyCategory.AirQuality, ReportSections.AirQuality)]
    [InlineData(HourlyCategory.Marine, ReportSections.Marine)]
    public void ToSection_MapsEveryCategory(HourlyCategory category, ReportSections expected) =>
        Assert.Equal(expected, category.ToSection());

    [Theory]
    [InlineData(ReportSections.Hourly, HourlyCategory.Marine, true)]
    [InlineData(ReportSections.Weather | ReportSections.Hourly, HourlyCategory.Weather, true)]
    [InlineData(ReportSections.Weather | ReportSections.Hourly, HourlyCategory.Marine, false)]
    [InlineData(ReportSections.Weather | ReportSections.AirQuality | ReportSections.Marine | ReportSections.Hourly, HourlyCategory.AirQuality, true)]
    public void IncludesHourlyFor_FollowsIncludeOrEverythingWhenHourlyIsAlone(ReportSections sections, HourlyCategory category, bool expected) =>
        Assert.Equal(expected, sections.IncludesHourlyFor(category));
}
