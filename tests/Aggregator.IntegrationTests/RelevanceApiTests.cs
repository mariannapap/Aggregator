using System.Text.Json;

namespace Aggregator.IntegrationTests;

[Requirement("R5")]
public sealed class RelevanceApiTests
{
    // Trimmed real geocoder answer for "Paris" in France: the capital comes back first but is not the nearest to Athens.
    private const string ParisResults = """
        {"results":[
          {"name":"Pariset","latitude":45.16,"longitude":5.67,"feature_code":"PPL","country_code":"FR","admin1":"Auvergne-Rhône-Alpes"},
          {"name":"Paris 15 Vaugirard","latitude":48.84,"longitude":2.30,"feature_code":"PPL","country_code":"FR","admin1":"Île-de-France Region"},
          {"name":"Paris","latitude":48.85,"longitude":2.35,"feature_code":"PPLC","country_code":"FR","admin1":"Île-de-France Region"},
          {"name":"Les Paris","latitude":45.62,"longitude":5.73,"feature_code":"PPL","country_code":"FR","admin1":"Auvergne-Rhône-Alpes"}
        ]}
        """;

    [Fact]
    public async Task DefaultSort_PutsTheCapitalNamedParisFirst()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.ScriptJson(OpenMeteoHosts.Geocoding, ParisResults);
        var client = await factory.CreateAuthorizedClientAsync();

        var body = await client.GetJsonAsync("/city-report/FR/Paris?include=weather");

        Assert.Equal(["Paris", "Pariset", "Paris 15 Vaugirard", "Les Paris"], Names(body));
    }

    [Fact]
    public async Task DistanceSort_KeepsTheOldNearestToTheAcropolisOrder()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.ScriptJson(OpenMeteoHosts.Geocoding, ParisResults);
        var client = await factory.CreateAuthorizedClientAsync();

        var body = await client.GetJsonAsync("/city-report/FR/Paris?include=weather&sort=distance");

        var distances = body.GetProperty("places").EnumerateArray().Select(place => place.GetProperty("distanceKm").GetDouble()).ToList();
        Assert.Equal(distances.Order(), distances);
        Assert.Equal("Pariset", Names(body)[0]);
    }

    private static List<string?> Names(JsonElement body) =>
        body.GetProperty("places").EnumerateArray().Select(place => place.GetProperty("city").GetString()).ToList();
}
