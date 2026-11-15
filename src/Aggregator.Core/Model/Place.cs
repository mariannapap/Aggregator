using System.ComponentModel;

namespace Aggregator.Core.Model;

[ImmutableObject(true)]
public sealed record Place(string Name, string Country, string? Region, Coordinates Coordinates);
