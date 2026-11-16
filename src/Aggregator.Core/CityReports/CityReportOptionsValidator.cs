using Microsoft.Extensions.Options;

namespace Aggregator.Core.CityReports;

public sealed class CityReportOptionsValidator : IValidateOptions<CityReportOptions>
{
    public ValidateOptionsResult Validate(string? name, CityReportOptions options) =>
        options.DefaultPlaceLimit <= options.MaxPlaceLimit
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"CityReport:DefaultPlaceLimit ({options.DefaultPlaceLimit}) must not exceed CityReport:MaxPlaceLimit ({options.MaxPlaceLimit}).");
}
