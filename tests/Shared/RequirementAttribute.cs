using Xunit.v3;

namespace Aggregator.Testing;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirementAttribute(string id) : Attribute, ITraitAttribute
{
    public const string TraitName = "Requirement";

    public string Id { get; } = id;

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [new(TraitName, Id)];
}
