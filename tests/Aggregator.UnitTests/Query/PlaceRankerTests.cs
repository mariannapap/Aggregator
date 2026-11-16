using Aggregator.Core.Model;
using Aggregator.Core.Query;

namespace Aggregator.UnitTests.Query;

[Requirement("R5")]
public sealed class PlaceRankerTests
{
    [Fact]
    public void Rank_ExactNameBeatsPrefixBeatsContainsBeatsNoMatch_WhateverTheType()
    {
        var exact = Rank("Kos", "AIRP", searched: "Kos");
        var prefix = Rank("Kosmas", "PPLC", searched: "Kos");
        var contains = Rank("Agios Kosmas", "PPLC", searched: "Kos");
        var noMatch = Rank("Gefyra", "PPLC", searched: "Kos");

        Assert.True(exact < prefix && prefix < contains && contains < noMatch, $"{exact} < {prefix} < {contains} < {noMatch}");
    }

    [Fact]
    public void Rank_SameNameMatch_OrdersCapitalThenSeatsByLevelThenTownThenAirport()
    {
        string[] codes = ["PPLC", "PPLA", "PPLA2", "PPLA3", "PPLA4", "PPL", "AIRP"];

        var ranks = codes.Select(code => Rank("Paris", code, searched: "Paris")).ToList();

        Assert.Equal(ranks.Order(), ranks);
        Assert.Equal(ranks.Count, ranks.Distinct().Count());
    }

    [Theory]
    [InlineData("Chánia", "chania")]
    [InlineData("CHANIA", "Chania")]
    [InlineData(" Chania ", "Chania")]
    public void Rank_NameMatch_IgnoresCaseAccentsAndSurroundingSpaces(string placeName, string searched) =>
        Assert.Equal(Rank("Chania", "PPL", searched: "Chania"), Rank(placeName, "PPL", searched));

    private static int Rank(string name, string featureCode, string searched) =>
        PlaceRanker.Rank(new Place(name, "GR", null, new Coordinates(0, 0), featureCode), searched);
}
