using System.ComponentModel;

namespace Aggregator.Core.Model;

// HybridCache checks only the cached type: immutable means it returns the stored instance instead of a defensive copy.
[ImmutableObject(true)]
public sealed record SourceResult<T>(SourceStatus Status, T? Value)
    where T : class
{
    public SourceResult<T> AsStale() => this with { Status = SourceStatus.Stale };
}
