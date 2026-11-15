using Aggregator.Core.Model;
using Aggregator.Core.Query;
using Microsoft.Extensions.Options;

namespace Aggregator.UnitTests.Query;

public sealed class RelevanceCalculatorTests
{
    private readonly RelevanceCalculator calculator =
        new(Options.Create(new RelevanceOptions { ReferenceLatitude = 37.9715, ReferenceLongitude = 23.7257 }));

    [Fact]
    public void DistanceKm_ReferencePoint_IsZero() =>
        Assert.Equal(0, calculator.DistanceKm(new Coordinates(37.9715, 23.7257)), 6);

    [Fact]
    public void DistanceKm_Thessaloniki_IsAboutThreeHundredKilometres()
    {
        var distance = calculator.DistanceKm(new Coordinates(40.6403, 22.9439));

        Assert.InRange(distance, 295, 310);
    }

    [Fact]
    public void DistanceKm_CloserPlace_IsSmaller()
    {
        var athensCentre = calculator.DistanceKm(new Coordinates(37.98376, 23.72784));
        var airport = calculator.DistanceKm(new Coordinates(37.93636, 23.94447));

        Assert.True(athensCentre < airport);
    }
}
