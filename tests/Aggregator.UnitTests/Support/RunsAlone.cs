namespace Aggregator.UnitTests.Support;

// CPU-heavy or timing-sensitive concurrency tests run on their own, after the parallel tests,
// so they neither starve the thread pool for other tests nor get starved by them.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RunsAlone
{
    public const string Name = "Concurrency tests, not run in parallel";
}
