using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace Aggregator.Api.CityReports;

internal sealed class CityReportQuery
{
    [FromRoute(Name = "country")]
    [Description("ISO 3166-1 alpha-2 country code, e.g. GR. Case-insensitive.")]
    public string Country { get; init; } = string.Empty;

    [FromRoute(Name = "city")]
    [Description("City or airport name, e.g. Athens. Every matching settlement and airport in the country is returned.")]
    public string City { get; init; } = string.Empty;

    [FromQuery(Name = "include")]
    [Description("Comma-separated sections: weather, airQuality, marine, hourly. Default: weather,airQuality,marine. Only the APIs needed for the requested sections are called. marine is no_data for places away from the sea. With hourly, rows come from the included sections (all three when include=hourly on its own).")]
    public string? Include { get; init; }

    [FromQuery(Name = "region")]
    [Description("Region filter, e.g. Crete. Fuzzy: case, accents and small typos are tolerated (krete → Crete, attiki → Attica); a partial name matches every region containing it. Ignored and reported in filters.region when nothing matches.")]
    public string? Region { get; init; }

    [FromQuery(Name = "sort")]
    [Description("relevance (distance from the Acropolis, nearest first; default), name or temperature. Prefix with '-' for descending. temperature requires include=weather; places without a temperature sort last.")]
    public string? Sort { get; init; }

    [FromQuery(Name = "hourly.category")]
    [Description("weather, airQuality or marine: narrows hourly rows to one section, which must also be in include (or include=hourly on its own). Requires include=hourly.")]
    public string? HourlyCategory { get; init; }

    [FromQuery(Name = "hourly.from")]
    [Description("ISO 8601 date-time, inclusive, e.g. 2026-11-15T18:00Z. Clamped to the hourly window that starts at the current hour (UTC; length set by CityReport:HourlyWindowHours); clamping is reported in filters.hourly. Requires include=hourly.")]
    public string? HourlyFrom { get; init; }

    [FromQuery(Name = "hourly.to")]
    [Description("ISO 8601 date-time, inclusive. Clamped to the hourly window; must not be before hourly.from. Requires include=hourly.")]
    public string? HourlyTo { get; init; }

    [FromQuery(Name = "hourly.sort")]
    [Description("time (default) or -time for latest first. Requires include=hourly.")]
    public string? HourlySort { get; init; }
}
