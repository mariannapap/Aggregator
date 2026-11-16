namespace Aggregator.Infrastructure.Caching;

// NoDataFreshFor: a shorter lifetime for no_data answers (null = FreshFor). FailureCooldown: after a failure, don't call
// the source again for this long and serve the last good value instead (zero = always try).
// CallBudget: how long one caller waits for the source, queue and retries included (null = no limit).
internal sealed record CacheTimes(TimeSpan FreshFor, TimeSpan KeepLastGoodFor, TimeSpan? NoDataFreshFor, TimeSpan FailureCooldown, TimeSpan? CallBudget = null);
