using Microsoft.Extensions.Options;

namespace Aggregator.Core.Statistics;

public sealed class AnomalyOptionsValidator(IOptions<StatisticsOptions> statisticsOptions) : IValidateOptions<AnomalyOptions>
{
    public ValidateOptionsResult Validate(string? name, AnomalyOptions options)
    {
        var failures = new List<string>();

        var minutesKept = statisticsOptions.Value.RecentMinutesKept;
        if (minutesKept < options.WindowMinutes + 1)
        {
            failures.Add(
                $"Statistics:RecentMinutesKept ({minutesKept}) must be at least Anomaly:WindowMinutes + 1 ({options.WindowMinutes + 1}), " +
                "so the history covers the whole anomaly window plus the current minute.");
        }

        if (options.Interval > TimeSpan.FromMinutes(options.WindowMinutes))
        {
            failures.Add(
                $"Anomaly:Interval ({options.Interval}) must not exceed Anomaly:WindowMinutes ({options.WindowMinutes} min), " +
                "otherwise some minutes are never analysed.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
