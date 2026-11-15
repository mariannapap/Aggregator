using Microsoft.Extensions.Options;

namespace Aggregator.Core.Statistics;

public sealed class StatisticsOptionsValidator : IValidateOptions<StatisticsOptions>
{
    public ValidateOptionsResult Validate(string? name, StatisticsOptions options) =>
        options.FastBelowMs <= options.SlowAboveMs
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Statistics:FastBelowMs ({options.FastBelowMs}) must not exceed Statistics:SlowAboveMs ({options.SlowAboveMs}).");
}
