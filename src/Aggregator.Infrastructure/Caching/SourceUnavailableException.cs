using System.Diagnostics.CodeAnalysis;

namespace Aggregator.Infrastructure.Caching;

[SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "Internal control flow inside StaleAwareCache: it never leaves the assembly.")]
internal sealed class SourceUnavailableException : Exception
{
    public SourceUnavailableException()
        : base("The source is unavailable.")
    {
    }
}
