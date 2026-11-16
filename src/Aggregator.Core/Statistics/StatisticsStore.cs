using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Aggregator.Core.Statistics;

public sealed class StatisticsStore(IOptions<StatisticsOptions> options, TimeProvider timeProvider, IEnumerable<MonitoredApi> monitoredApis)
    : IStatisticsRecorder, IStatisticsReader
{
    private readonly ConcurrentDictionary<string, ApiStatistics> statistics = new(
        monitoredApis
            .Select(api => api.Name)
            .Distinct(StringComparer.Ordinal)
            .Select(name => KeyValuePair.Create(name, new ApiStatistics(options.Value.RecentMinutesKept))),
        StringComparer.Ordinal);

    public IReadOnlyList<string> Apis => statistics.Keys.Order(StringComparer.Ordinal).ToList();

    public void Record(string api, TimeSpan duration, bool succeeded)
    {
        var settings = options.Value;
        var milliseconds = duration.TotalMilliseconds;
        var bucket = milliseconds < settings.FastBelowMs ? Bucket.Fast
            : milliseconds > settings.SlowAboveMs ? Bucket.Slow
            : Bucket.Average;

        statistics.GetOrAdd(api, _ => new ApiStatistics(settings.RecentMinutesKept)).Record(milliseconds, bucket, succeeded, CurrentMinute());
    }

    public IReadOnlyList<ApiStatisticsSnapshot> Snapshot() =>
        statistics.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => entry.Value.Snapshot(entry.Key)).ToList();

    public WindowSample Total(string api) =>
        statistics.TryGetValue(api, out var stats) ? stats.Total() : new WindowSample(0, 0);

    public WindowSample Recent(string api, int minutes) =>
        statistics.TryGetValue(api, out var stats) ? stats.Recent(CurrentMinute(), minutes) : new WindowSample(0, 0);

    private long CurrentMinute() => timeProvider.GetUtcNow().ToUnixTimeSeconds() / 60;

    private enum Bucket
    {
        Fast,
        Average,
        Slow
    }

    private sealed record Totals(
        long Count,
        long FailedCount,
        double TotalMs,
        long FastCount,
        double FastMs,
        long AverageCount,
        double AverageMs,
        long SlowCount,
        double SlowMs)
    {
        public static Totals Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);

        public Totals Add(double milliseconds, Bucket bucket, bool succeeded)
        {
            var updated = this with
            {
                Count = Count + 1,
                FailedCount = succeeded ? FailedCount : FailedCount + 1,
                TotalMs = TotalMs + milliseconds
            };
            return bucket switch
            {
                Bucket.Fast => updated with { FastCount = FastCount + 1, FastMs = FastMs + milliseconds },
                Bucket.Slow => updated with { SlowCount = SlowCount + 1, SlowMs = SlowMs + milliseconds },
                _ => updated with { AverageCount = AverageCount + 1, AverageMs = AverageMs + milliseconds }
            };
        }
    }

    private sealed record MinuteSlot(long Minute, long Count, double TotalMs);

    private sealed class ApiStatistics(int minutesKept)
    {
        private readonly MinuteSlot?[] slots = new MinuteSlot?[minutesKept];
        private Totals totals = Totals.Empty;

        public void Record(double milliseconds, Bucket bucket, bool succeeded, long minute)
        {
            Totals current;
            do
            {
                current = Volatile.Read(ref totals);
            }
            while (!ReferenceEquals(Interlocked.CompareExchange(ref totals, current.Add(milliseconds, bucket, succeeded), current), current));

            var index = (int)(minute % slots.Length);
            MinuteSlot? slot;
            MinuteSlot updated;
            do
            {
                slot = Volatile.Read(ref slots[index]);
                updated = slot is not null && slot.Minute == minute
                    ? slot with { Count = slot.Count + 1, TotalMs = slot.TotalMs + milliseconds }
                    : new MinuteSlot(minute, 1, milliseconds);
            }
            while (!ReferenceEquals(Interlocked.CompareExchange(ref slots[index], updated, slot), slot));
        }

        public WindowSample Total()
        {
            var current = Volatile.Read(ref totals);
            return new WindowSample(current.Count, current.TotalMs);
        }

        public WindowSample Recent(long currentMinute, int minutes)
        {
            long count = 0;
            double total = 0;
            for (var index = 0; index < slots.Length; index++)
            {
                var slot = Volatile.Read(ref slots[index]);
                if (slot is not null && slot.Minute > currentMinute - minutes && slot.Minute <= currentMinute)
                {
                    count += slot.Count;
                    total += slot.TotalMs;
                }
            }

            return new WindowSample(count, total);
        }

        public ApiStatisticsSnapshot Snapshot(string api)
        {
            var current = Volatile.Read(ref totals);
            return new ApiStatisticsSnapshot(
                api,
                current.Count,
                current.FailedCount,
                Average(current.TotalMs, current.Count),
                new BucketSnapshot(current.FastCount, Average(current.FastMs, current.FastCount)),
                new BucketSnapshot(current.AverageCount, Average(current.AverageMs, current.AverageCount)),
                new BucketSnapshot(current.SlowCount, Average(current.SlowMs, current.SlowCount)));
        }

        private static double? Average(double total, long count) => count == 0 ? null : Math.Round(total / count, 2);
    }
}
