using System.ComponentModel;

namespace Aggregator.Core.Model;

[ImmutableObject(true)]
public sealed record SourceResult<T>(SourceStatus Status, T? Value)
    where T : class
{
    public SourceResult<T> AsStale() => this with { Status = SourceStatus.Stale };
}
