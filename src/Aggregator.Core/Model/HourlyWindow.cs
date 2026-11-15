namespace Aggregator.Core.Model;

public sealed record HourlyWindow(DateTimeOffset Start, int Hours)
{
    public DateTimeOffset End => Start.AddHours(Hours);

    public DateTimeOffset LastHour => Start.AddHours(Hours - 1);

    public static HourlyWindow StartingAtCurrentHour(DateTimeOffset now, int hours)
    {
        var utc = now.ToUniversalTime();
        var currentHour = new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
        return new HourlyWindow(currentHour, hours);
    }

    public bool Contains(DateTimeOffset time) => time >= Start && time < End;
}
