using Aggregator.Api.CityReports;
using Aggregator.Core.CityReports;
using Aggregator.Core.Model;
using Microsoft.Extensions.Options;

namespace Aggregator.UnitTests.Api;

public sealed class CityReportQueryParserTests
{
    private readonly CityReportQueryParser parser =
        new(Options.Create(new CityReportOptions { MaxCityLength = 100, MaxRegionLength = 20 }));

    [Fact]
    public void Parse_Defaults_IncludeEveryDataSectionSortedByRelevance()
    {
        var result = parser.Parse(new CityReportQuery { Country = "gr", City = " Athens " });

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(("Athens", "GR"), (result.Request.City, result.Request.Country));
        Assert.Equal(ReportSections.Weather | ReportSections.AirQuality | ReportSections.Marine, result.Request.Sections);
        Assert.Equal(PlaceSort.Default, result.Request.Sort);
    }

    [Fact]
    public void Parse_FullQuery_MapsEveryOption()
    {
        var result = parser.Parse(new CityReportQuery
        {
            Country = "GR",
            City = "Chania",
            Include = "Weather, MARINE, HOURLY",
            Region = "crete",
            Sort = "-Temperature",
            HourlyCategory = "Marine",
            HourlyFrom = "2026-11-15T18:00Z",
            HourlyTo = "2026-11-15T20:00Z",
            HourlySort = "-time"
        });

        Assert.True(result.IsValid);
        var request = result.Request;
        Assert.Equal(ReportSections.Weather | ReportSections.Marine | ReportSections.Hourly, request.Sections);
        Assert.Equal("crete", request.Region);
        Assert.Equal(new PlaceSort(PlaceSortField.Temperature, true), request.Sort);
        Assert.Equal(HourlyCategory.Marine, request.Hourly.Category);
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 18, 0, 0, TimeSpan.Zero), request.Hourly.From);
        Assert.True(request.Hourly.SortDescending);
    }

    [Theory]
    [InlineData("GRC", "Athens", null, null, "country")]
    [InlineData("G1", "Athens", null, null, "country")]
    [InlineData("GR", " ", null, null, "city")]
    [InlineData("GR", "Athens", "weather,uv", null, "include")]
    [InlineData("GR", "Athens", null, "population", "sort")]
    [InlineData("GR", "Athens", null, "1", "sort")]
    [InlineData("GR", "Athens", "airQuality", "temperature", "sort")]
    public void Parse_InvalidInput_ReportsTheField(string country, string city, string? include, string? sort, string field)
    {
        var result = parser.Parse(new CityReportQuery { Country = country, City = city, Include = include, Sort = sort });

        Assert.False(result.IsValid);
        Assert.Null(result.Request);
        Assert.Contains(field, result.Errors.Keys);
    }

    [Fact]
    public void Parse_HourlyParametersWithoutHourly_AreRejected() =>
        Assert.Contains("hourly", parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", HourlySort = "time" }).Errors.Keys);

    [Theory]
    [InlineData("hourly.category", "uv", null, null, null)]
    [InlineData("hourly.category", "0", null, null, null)]
    [InlineData("hourly.from", null, "yesterday", null, null)]
    [InlineData("hourly.from", null, "2026-11-15T20:00Z", "2026-11-15T18:00Z", null)]
    [InlineData("hourly.sort", null, null, null, "value")]
    public void Parse_InvalidHourlyOptions_ReportsTheField(string field, string? category, string? from, string? to, string? sort)
    {
        var result = parser.Parse(new CityReportQuery
        {
            Country = "GR",
            City = "Athens",
            Include = "hourly",
            HourlyCategory = category,
            HourlyFrom = from,
            HourlyTo = to,
            HourlySort = sort
        });

        Assert.Contains(field, result.Errors.Keys);
    }

    [Theory]
    [InlineData("weather,hourly", "airQuality", false)]
    [InlineData("weather,airQuality,hourly", "marine", false)]
    [InlineData("weather,airQuality,hourly", "airQuality", true)]
    [InlineData("hourly", "marine", true)]
    [InlineData("hourly", "weather", true)]
    public void Parse_HourlyCategory_MustBeAnIncludedSectionUnlessHourlyIsOnItsOwn(string include, string category, bool valid)
    {
        var result = parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", Include = include, HourlyCategory = category });

        Assert.Equal(valid, result.IsValid);
        if (!valid)
            Assert.Contains("requires that section in include", Assert.Single(result.Errors["hourly.category"]), StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_CityLongerThanConfiguredLimit_IsRejected() =>
        Assert.Contains("city", parser.Parse(new CityReportQuery { Country = "GR", City = new string('a', 101) }).Errors.Keys);

    [Fact]
    public void Parse_RegionLongerThanConfiguredLimit_IsRejected() =>
        Assert.Contains("region", parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", Region = new string('a', 21) }).Errors.Keys);

    [Fact]
    public void Parse_SeveralProblems_ReportsAllOfThem()
    {
        var result = parser.Parse(new CityReportQuery { Country = "GRC", City = "", Sort = "population" });

        Assert.Equal(["city", "country", "sort"], result.Errors.Keys.Order(StringComparer.Ordinal));
    }
}
