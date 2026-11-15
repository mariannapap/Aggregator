namespace Aggregator.Core.Model;

public static class SourceResult
{
    public static SourceResult<T> Ok<T>(T value)
        where T : class => new(SourceStatus.Ok, value);

    public static SourceResult<T> Partial<T>(T value)
        where T : class => new(SourceStatus.Partial, value);

    public static SourceResult<T> NoData<T>()
        where T : class => new(SourceStatus.NoData, null);

    public static SourceResult<T> Unavailable<T>()
        where T : class => new(SourceStatus.Unavailable, null);
}
