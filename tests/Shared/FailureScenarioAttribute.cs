using Xunit.v3;

namespace Aggregator.Testing;

[AttributeUsage(AttributeTargets.Method)]
public sealed class FailureScenarioAttribute : Attribute, ITraitAttribute
{
    public const string TraitName = "Scenario";
    public const string TraitValue = "Failure";

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [new(TraitName, TraitValue)];
}
