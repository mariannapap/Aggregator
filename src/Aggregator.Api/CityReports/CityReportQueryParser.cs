using System.Collections.Frozen;
using System.Globalization;
using Aggregator.Core.CityReports;
using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Microsoft.Extensions.Options;

namespace Aggregator.Api.CityReports;

internal sealed class CityReportQueryParser(IOptions<CityReportOptions> options)
{
    private const string HourlyToken = "hourly";
    private const ReportSections DefaultSections = ReportSections.Weather | ReportSections.AirQuality;

    private static readonly FrozenDictionary<string, ReportSections> Sections = new Dictionary<string, ReportSections>
    {
        [SourceNames.Weather] = ReportSections.Weather,
        [SourceNames.AirQuality] = ReportSections.AirQuality,
        [HourlyToken] = ReportSections.Hourly
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, PlaceSortField> SortFields = new Dictionary<string, PlaceSortField>
    {
        ["relevance"] = PlaceSortField.Relevance,
        ["name"] = PlaceSortField.Name,
        ["temperature"] = PlaceSortField.Temperature
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, HourlyCategory> HourlyCategories = new Dictionary<string, HourlyCategory>
    {
        [SourceNames.Weather] = HourlyCategory.Weather,
        [SourceNames.AirQuality] = HourlyCategory.AirQuality
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, bool> HourlySortDescending = new Dictionary<string, bool>
    {
        ["time"] = false,
        ["-time"] = true
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public ParseResult Parse(CityReportQuery query)
    {
        var settings = options.Value;
        var errors = new Dictionary<string, string[]>();

        var country = query.Country.Trim().ToUpperInvariant();
        if (country.Length != 2 || !country.All(char.IsAsciiLetter))
            errors["country"] = ["Country must be an ISO 3166-1 alpha-2 code, e.g. GR."];

        var city = query.City.Trim();
        if (city.Length == 0 || city.Length > settings.MaxCityLength)
            errors["city"] = [$"City must be between 1 and {settings.MaxCityLength} characters."];

        var sections = ParseSections(query.Include, errors);

        var region = string.IsNullOrWhiteSpace(query.Region) ? null : query.Region.Trim();
        if (region is not null && region.Length > settings.MaxRegionLength)
            errors["region"] = [$"Region must be at most {settings.MaxRegionLength} characters."];

        var sort = ParseSort(query.Sort, sections, errors);
        var hourly = ParseHourly(query, sections, errors);

        return errors.Count > 0
            ? new ParseResult(null, errors)
            : new ParseResult(new CityReportRequest(city, country, sections, region, sort, hourly), errors);
    }

    private static ReportSections ParseSections(string? include, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(include))
            return DefaultSections;

        var sections = ReportSections.None;
        foreach (var token in include.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Sections.TryGetValue(token, out var section))
            {
                errors["include"] = ["Allowed values: weather, airQuality, hourly (comma-separated)."];
                return DefaultSections;
            }

            sections |= section;
        }

        return sections;
    }

    private static PlaceSort ParseSort(string? value, ReportSections sections, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return PlaceSort.Default;

        var descending = value.StartsWith('-');
        if (!SortFields.TryGetValue(value.TrimStart('-').Trim(), out var field))
        {
            errors["sort"] = ["Allowed values: relevance, name, temperature; prefix with '-' for descending."];
            return PlaceSort.Default;
        }

        if (field == PlaceSortField.Temperature && !sections.HasFlag(ReportSections.Weather))
            errors["sort"] = ["sort=temperature requires include=weather."];

        return new PlaceSort(field, descending);
    }

    private static HourlyOptions ParseHourly(CityReportQuery query, ReportSections sections, Dictionary<string, string[]> errors)
    {
        var anyHourlyParameter = query.HourlyCategory is not null || query.HourlyFrom is not null || query.HourlyTo is not null || query.HourlySort is not null;
        if (!sections.HasFlag(ReportSections.Hourly))
        {
            if (anyHourlyParameter)
                errors[HourlyToken] = ["hourly.* parameters require include=hourly."];
            return HourlyOptions.Default;
        }

        HourlyCategory? category = null;
        if (!string.IsNullOrWhiteSpace(query.HourlyCategory))
        {
            if (HourlyCategories.TryGetValue(query.HourlyCategory.Trim(), out var parsed))
                category = parsed;
            else
                errors["hourly.category"] = ["Allowed values: weather, airQuality."];
        }

        var from = ParseTime(query.HourlyFrom, "hourly.from", errors);
        var to = ParseTime(query.HourlyTo, "hourly.to", errors);
        if (from is not null && to is not null && from > to)
            errors["hourly.from"] = ["hourly.from must not be after hourly.to."];

        var descending = false;
        if (!string.IsNullOrWhiteSpace(query.HourlySort) && !HourlySortDescending.TryGetValue(query.HourlySort.Trim(), out descending))
            errors["hourly.sort"] = ["Allowed values: time, -time."];

        return new HourlyOptions(category, from, to, descending);
    }

    private static DateTimeOffset? ParseTime(string? value, string name, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return parsed;

        errors[name] = ["Must be an ISO 8601 date-time, e.g. 2026-11-15T18:00Z."];
        return null;
    }
}
