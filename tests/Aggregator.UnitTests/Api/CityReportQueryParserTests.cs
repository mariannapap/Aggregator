using Aggregator.Api.CityReports;
using Aggregator.Core.CityReports;
using Aggregator.Core.Model;
using Microsoft.Extensions.Options;

namespace Aggregator.UnitTests.Api;

[Requirement("R5")]
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
    [Requirement("R6"), FailureScenario]
    public void Parse_InvalidInput_ReportsTheField(string country, string city, string? include, string? sort, string field)
    {
        var result = parser.Parse(new CityReportQuery { Country = country, City = city, Include = include, Sort = sort });

        Assert.False(result.IsValid);
        Assert.Null(result.Request);
        Assert.Contains(field, result.Errors.Keys);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public void Parse_HourlyParametersWithoutHourly_AreRejected() =>
        Assert.Contains("hourly", parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", HourlySort = "time" }).Errors.Keys);

    [Theory]
    [InlineData("hourly.category", "uv", null, null, null)]
    [InlineData("hourly.category", "0", null, null, null)]
    [InlineData("hourly.from", null, "yesterday", null, null)]
    [InlineData("hourly.from", null, "2026-11-15T20:00Z", "2026-11-15T18:00Z", null)]
    [InlineData("hourly.sort", null, null, null, "value")]
    [Requirement("R6"), FailureScenario]
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
    [InlineData("weather,hourly", "airQuality")]
    [InlineData("weather,airQuality,hourly", "marine")]
    [Requirement("R6"), FailureScenario]
    public void Parse_HourlyCategoryNotInInclude_IsRejected(string include, string category)
    {
        var result = parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", Include = include, HourlyCategory = category });

        Assert.Contains("requires that section in include", Assert.Single(result.Errors["hourly.category"]), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("weather,airQuality,hourly", "airQuality")]
    [InlineData("hourly", "marine")]
    [InlineData("hourly", "weather")]
    public void Parse_HourlyCategoryInIncludeOrHourlyOnItsOwn_IsAccepted(string include, string category) =>
        Assert.True(parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", Include = include, HourlyCategory = category }).IsValid);

    [Fact]
    [Requirement("R6"), FailureScenario]
    public void Parse_InvalidInclude_ReportsOnlyIncludeNotTheChecksThatDependOnIt()
    {
        var result = parser.Parse(new CityReportQuery
        {
            Country = "GR",
            City = "Athens",
            Include = "weather,uv",
            Sort = "temperature",
            HourlyCategory = "marine",
            HourlyFrom = "2026-11-15T18:00Z"
        });

        Assert.Equal(["include"], result.Errors.Keys);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public void Parse_InvalidInclude_StillValidatesIndependentValues()
    {
        var result = parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", Include = "uv", Sort = "population", HourlyFrom = "soon" });

        Assert.Equal(["hourly.from", "include", "sort"], result.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(",")]
    [InlineData(" , ,")]
    [Requirement("R6"), FailureScenario]
    public void Parse_IncludeWithoutSections_IsRejected(string include) =>
        Assert.Contains("at least one section", Assert.Single(parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", Include = include }).Errors["include"]), StringComparison.Ordinal);

    [Fact]
    public void Parse_EmptyHourlyParameters_DoNotCountAsPresent() =>
        Assert.True(parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", HourlySort = "", HourlyFrom = " " }).IsValid);

    [Theory]
    [InlineData("--name")]
    [InlineData("- name")]
    [InlineData("name-")]
    [Requirement("R6"), FailureScenario]
    public void Parse_SortWithMoreThanOneLeadingDash_IsRejected(string sort) =>
        Assert.Contains("sort", parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", Sort = sort }).Errors.Keys);

    [Theory]
    [InlineData("-name", true)]
    [InlineData(" name ", false)]
    public void Parse_SortWithOptionalDash_SetsTheDirection(string sort, bool descending) =>
        Assert.Equal(new PlaceSort(PlaceSortField.Name, descending), parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", Sort = sort }).Request?.Sort);

    [Theory]
    [InlineData("2026-11-15T18:00Z", 18)]
    [InlineData("2026-11-15T18:00:00Z", 18)]
    [InlineData("2026-11-15T20:00+02:00", 18)]
    [InlineData("2026-11-15T20:00 02:00", 18)]
    [InlineData("2026-11-15T20:00:00 02:00", 18)]
    [InlineData("2026-11-15T18:00", 18)]
    [InlineData("2026-11-15", 0)]
    public void Parse_IsoTimestamp_IsAcceptedAsUtc(string value, int utcHour)
    {
        var result = parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", Include = "hourly", HourlyFrom = value });

        Assert.Equal(new DateTimeOffset(2026, 11, 15, utcHour, 0, 0, TimeSpan.Zero), result.Request?.Hourly.From);
    }

    [Theory]
    [InlineData("11/15/2026")]
    [InlineData("15/11/2026 18:00")]
    [InlineData("Nov 15 2026")]
    [InlineData("2026-11-15 18:00")]
    [Requirement("R6"), FailureScenario]
    public void Parse_NonIsoTimestamp_IsRejected(string value) =>
        Assert.Contains("hourly.from", parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", Include = "hourly", HourlyFrom = value }).Errors.Keys);

    [Fact]
    [Requirement("R6"), FailureScenario]
    public void Parse_CityLongerThanConfiguredLimit_IsRejected() =>
        Assert.Contains("city", parser.Parse(new CityReportQuery { Country = "GR", City = new string('a', 101) }).Errors.Keys);

    [Fact]
    [Requirement("R6"), FailureScenario]
    public void Parse_RegionLongerThanConfiguredLimit_IsRejected() =>
        Assert.Contains("region", parser.Parse(new CityReportQuery { Country = "GR", City = "Athens", Region = new string('a', 21) }).Errors.Keys);

    [Fact]
    [Requirement("R6"), FailureScenario]
    public void Parse_SeveralProblems_ReportsAllOfThem()
    {
        var result = parser.Parse(new CityReportQuery { Country = "GRC", City = "", Sort = "population" });

        Assert.Equal(["city", "country", "sort"], result.Errors.Keys.Order(StringComparer.Ordinal));
    }
}
