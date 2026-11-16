using System.ComponentModel;

namespace Aggregator.Core.Model;

// Cached inside SourceResult<T>: must stay immutable for its [ImmutableObject] promise to hold.
[ImmutableObject(true)]
public sealed record Place(string Name, string Country, string? Region, Coordinates Coordinates);
