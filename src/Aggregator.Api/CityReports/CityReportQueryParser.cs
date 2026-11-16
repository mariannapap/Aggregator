using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using Aggregator.Core.CityReports;
using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Microsoft.Extensions.Options;

namespace Aggregator.Api.CityReports;

internal sealed partial class CityReportQueryParser(IOptions<CityReportOptions> options)
{
    private const string HourlyToken = "hourly";
    private const ReportSections DefaultSections = ReportSectionsExtensions.DataSections;
    private const string IncludeValues = "Allowed values: weather, airQuality, marine, hourly (comma-separated).";

    private static readonly string[] IsoFormats =
    [
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
        "yyyy-MM-dd'T'HH:mm",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd"
    ];

    private static readonly FrozenDictionary<string, ReportSections> Sections = new Dictionary<string, ReportSections>
    {
        [SourceNames.Weather] = ReportSections.Weather,
        [SourceNames.AirQuality] = ReportSections.AirQuality,
        [SourceNames.Marine] = ReportSections.Marine,
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
        [SourceNames.AirQuality] = HourlyCategory.AirQuality,
        [SourceNames.Marine] = HourlyCategory.Marine
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
            : new ParseResult(new CityReportRequest(city, country, sections ?? DefaultSections, region, sort, hourly), errors);
    }

    // Null when include is invalid: checks that depend on the sections are then skipped.
    private static ReportSections? ParseSections(string? include, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(include))
            return DefaultSections;

        var sections = ReportSections.None;
        foreach (var token in include.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Sections.TryGetValue(token, out var section))
            {
                errors["include"] = [IncludeValues];
                return null;
            }

            sections |= section;
        }

        if (sections != ReportSections.None)
            return sections;

        errors["include"] = [$"include must name at least one section. {IncludeValues}"];
        return null;
    }

    private static PlaceSort ParseSort(string? value, ReportSections? sections, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return PlaceSort.Default;

        var trimmed = value.Trim();
        var descending = trimmed.StartsWith('-');
        if (!SortFields.TryGetValue(descending ? trimmed[1..] : trimmed, out var field))
        {
            errors["sort"] = ["Allowed values: relevance, name, temperature; prefix with one '-' for descending."];
            return PlaceSort.Default;
        }

        if (field == PlaceSortField.Temperature && sections is { } included && !included.HasFlag(ReportSections.Weather))
            errors["sort"] = ["sort=temperature requires include=weather."];

        return new PlaceSort(field, descending);
    }

    private static HourlyOptions ParseHourly(CityReportQuery query, ReportSections? sections, Dictionary<string, string[]> errors)
    {
        var anyHourlyParameter = !string.IsNullOrWhiteSpace(query.HourlyCategory)
            || !string.IsNullOrWhiteSpace(query.HourlyFrom)
            || !string.IsNullOrWhiteSpace(query.HourlyTo)
            || !string.IsNullOrWhiteSpace(query.HourlySort);
        if (sections is { } included && !included.HasFlag(ReportSections.Hourly))
        {
            if (anyHourlyParameter)
                errors[HourlyToken] = ["hourly.* parameters require include=hourly."];
            return HourlyOptions.Default;
        }

        HourlyCategory? category = null;
        if (!string.IsNullOrWhiteSpace(query.HourlyCategory))
        {
            if (!HourlyCategories.TryGetValue(query.HourlyCategory.Trim(), out var parsed))
                errors["hourly.category"] = ["Allowed values: weather, airQuality, marine."];
            else if (sections is { } withHourly && !withHourly.IncludesHourlyFor(parsed))
                errors["hourly.category"] = [$"hourly.category={query.HourlyCategory.Trim()} requires that section in include (or include=hourly on its own)."];
            else
                category = parsed;
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

        // An unencoded '+' in a query string arrives as a space: "…T18:00 02:00" means "…T18:00+02:00".
        var candidate = SpaceBeforeOffset().Replace(value.Trim(), "+");
        if (DateTimeOffset.TryParseExact(candidate, IsoFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return parsed;

        errors[name] = ["Must be an ISO 8601 date-time, e.g. 2026-11-15T18:00Z or 2026-11-15T20:00+02:00."];
        return null;
    }

    [GeneratedRegex(@"(?<=T\d{2}:\d{2}(:\d{2}(\.\d+)?)?) (?=\d{2}:\d{2}$)")]
    private static partial Regex SpaceBeforeOffset();
}
