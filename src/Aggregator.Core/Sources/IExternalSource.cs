using Aggregator.Core.Model;

namespace Aggregator.Core.Sources;

public interface IExternalSource<in TQuery, TResult>
    where TQuery : ISourceQuery
    where TResult : class
{
    string Name { get; }

    Task<SourceResult<TResult>> FetchAsync(TQuery query, CancellationToken cancellationToken);
}
