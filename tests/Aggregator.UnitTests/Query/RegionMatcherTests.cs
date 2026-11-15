using Aggregator.Core.Query;
using Microsoft.Extensions.Options;

namespace Aggregator.UnitTests.Query;

public sealed class RegionMatcherTests
{
    private static readonly string?[] GreekRegions =
        ["Attica", "Crete", "Central Macedonia", "East Macedonia and Thrace", "Peloponnese", null];

    private readonly RegionMatcher matcher = new(Options.Create(new RegionMatchingOptions { MaxEditDistanceRatio = 0.3 }));

    [Theory]
    [InlineData("crete", "Crete")]
    [InlineData("  ATTICA ", "Attica")]
    [InlineData("krete", "Crete")]
    [InlineData("attiki", "Attica")]
    [InlineData("Pelopónnisos", "Peloponnese")]
    public void Match_SingleRegion_ReturnsIt(string requested, string expected)
    {
        var match = matcher.Match(requested, GreekRegions);

        Assert.True(match.Applied);
        Assert.Equal([expected], match.Matched);
    }

    [Fact]
    public void Match_ExactMatch_WinsOverContains()
    {
        var match = matcher.Match("Crete", ["Crete", "North Crete"]);

        Assert.Equal(["Crete"], match.Matched);
    }

    [Fact]
    public void Match_PartialName_ReturnsEveryContainingRegion()
    {
        var match = matcher.Match("macedonia", GreekRegions);

        Assert.Equal(["Central Macedonia", "East Macedonia and Thrace"], match.Matched);
    }

    [Fact]
    public void Match_NothingClose_IsNotApplied()
    {
        var match = matcher.Match("xyz", GreekRegions);

        Assert.False(match.Applied);
        Assert.Empty(match.Matched);
        Assert.Equal("xyz", match.Requested);
    }

    [Theory]
    [InlineData("", "", 0)]
    [InlineData("crete", "krete", 1)]
    [InlineData("attiki", "attica", 2)]
    [InlineData("abc", "", 3)]
    public void LevenshteinDistance_ReturnsEditCount(string source, string target, int expected) =>
        Assert.Equal(expected, RegionMatcher.LevenshteinDistance(source, target));

    [Fact]
    public void Normalize_StripsDiacriticsAndCase() =>
        Assert.Equal("athina", RegionMatcher.Normalize(" Athína "));
}
