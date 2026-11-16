using System.Net;
using System.Text.Json;

namespace Aggregator.IntegrationTests;

[Requirement("R4")]
public sealed class PagingApiTests
{
    private const string Report = "/city-report/GR/Anywhere";
    private const int Places = 30;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ByDefault_ReturnsTheConfiguredLimit_AndCallsTheLocationApisOnlyForThose()
    {
        await using var factory = CreateFactory();
        var client = await factory.CreateAuthorizedClientAsync();

        var body = await client.GetJsonAsync(Report);

        Assert.Equal(20, body.GetProperty("places").GetArrayLength());
        Assert.Equal((Places, true), Filters(body));
        Assert.All(
            [OpenMeteoHosts.Weather, OpenMeteoHosts.AirQuality, OpenMeteoHosts.Marine],
            host => Assert.Equal(20, factory.OpenMeteo.CallsTo(host)));
    }

    [Fact]
    public async Task LimitAndOffset_ReturnTheMatchingSliceOfTheFullOrder()
    {
        await using var factory = CreateFactory();
        var client = await factory.CreateAuthorizedClientAsync();

        var all = Names(await client.GetJsonAsync($"{Report}?limit=100"));
        var page = await client.GetJsonAsync($"{Report}?limit=5&offset=5");

        Assert.Equal(Places, all.Count);
        Assert.Equal(all.Skip(5).Take(5), Names(page));
        Assert.Equal((Places, true), Filters(page));
    }

    [Fact]
    [Requirement("R5")]
    public async Task SortByTemperatureWithALimit_RanksEveryPlace_ButCallsTheOtherApisOnlyForThePage()
    {
        await using var factory = CreateFactory();
        var client = await factory.CreateAuthorizedClientAsync();

        var body = await client.GetJsonAsync($"{Report}?sort=temperature&limit=3");

        Assert.Equal(["Place 0", "Place 1", "Place 2"], Names(body));
        Assert.Equal(Places, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Weather));
        Assert.Equal((3, 3), (factory.OpenMeteo.CallsTo(OpenMeteoHosts.AirQuality), factory.OpenMeteo.CallsTo(OpenMeteoHosts.Marine)));
    }

    [Fact]
    public async Task OffsetPastTheEnd_Returns200WithNoPlaces()
    {
        await using var factory = CreateFactory();
        var client = await factory.CreateAuthorizedClientAsync();

        var body = await client.GetJsonAsync($"{Report}?offset=100");

        Assert.Equal(0, body.GetProperty("places").GetArrayLength());
        Assert.Equal((Places, true), Filters(body));
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    [InlineData("offset=-1")]
    [Requirement("R6"), FailureScenario]
    public async Task InvalidPaging_Returns400(string paging)
    {
        await using var factory = CreateFactory();
        var client = await factory.CreateAuthorizedClientAsync();

        var response = await client.GetAsync(new Uri($"{Report}?{paging}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.OpenMeteo.Calls);
    }

    private static AggregatorApiFactory CreateFactory()
    {
        var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = Places;
        return factory;
    }

    private static (int Total, bool Truncated) Filters(JsonElement body)
    {
        var filters = body.GetProperty("filters");
        return (filters.GetProperty("totalPlaces").GetInt32(), filters.GetProperty("truncated").GetBoolean());
    }

    private static List<string?> Names(JsonElement body) =>
        body.GetProperty("places").EnumerateArray().Select(place => place.GetProperty("city").GetString()).ToList();
}
