using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Infrastructure.Caching;
using Aggregator.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aggregator.IntegrationTests;

public sealed class StartupTests
{
    [Theory]
    [InlineData("Statistics:FastBelowMs", "300", "FastBelowMs")]
    [InlineData("Anomaly:WindowMinutes", "30", "RecentMinutesKept")]
    [InlineData("CityReport:HourlyWindowHours", "0", "HourlyWindowHours")]
    [InlineData("HttpClients:Weather:BaseUrl", "", "BaseUrl")]
    [InlineData("HttpClients:Weather:BaseUrl", "https://api.open-meteo.com/v1", "ends with '/'")]
    [InlineData("CityReport:MaxPlaceLimit", "10", "DefaultPlaceLimit")]
    [InlineData("Cache:SizeLimitMegabytes", "0", "SizeLimitMegabytes")]
    [InlineData("CityReport:CoordinateDecimals", "5", "CoordinateDecimals")]
    [Requirement("R6"), FailureScenario]
    public async Task InvalidConfiguration_StopsTheAppAtStartup(string key, string value, string expectedInMessage)
    {
        await using var factory = new AggregatorApiFactory();

        var exception = Assert.ThrowsAny<Exception>(() => factory.WithSettings((key, value)).CreateClient());

        Assert.Contains(expectedInMessage, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("R2")]
    public async Task EveryExternalSource_IsResolvedAsTheCachingDecoratorAroundItsAdapter()
    {
        await using var factory = new AggregatorApiFactory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        AssertDecorated<GeocodingQuery, IReadOnlyList<Place>>(services, SourceNames.Geocoding);
        AssertDecorated<LocationQuery, Weather>(services, SourceNames.Weather);
        AssertDecorated<LocationQuery, AirQuality>(services, SourceNames.AirQuality);
        AssertDecorated<LocationQuery, Marine>(services, SourceNames.Marine);
        AssertDecorated<AreaQuery, Hazards>(services, SourceNames.Hazards);
    }

    [Fact]
    [Requirement("R6")]
    public async Task EveryClient_TakesItsCallBudgetFromItsOwnTotalRequestTimeout()
    {
        await using var factory = new AggregatorApiFactory();
        var app = factory.WithSettings(("HttpClients:Hazards:Resilience:TotalRequestTimeout:Timeout", "00:00:21"));
        var options = app.Services.GetRequiredService<IOptionsMonitor<SourceClientOptions>>();
        var clients = app.Services.GetRequiredService<IHttpClientFactory>();

        Assert.Equal(TimeSpan.FromSeconds(21), options.Get(SourceNames.Hazards).CallBudget);
        Assert.Equal(TimeSpan.FromSeconds(21), clients.CreateClient(SourceNames.Hazards).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(8), options.Get(SourceNames.Weather).CallBudget);
        Assert.Equal(TimeSpan.FromSeconds(8), clients.CreateClient(SourceNames.Weather).Timeout);
    }

    private static void AssertDecorated<TQuery, TResult>(IServiceProvider services, string expectedName)
        where TQuery : ISourceQuery
        where TResult : class
    {
        var source = services.GetRequiredService<IExternalSource<TQuery, TResult>>();

        Assert.IsType<CachingSource<TQuery, TResult>>(source);
        Assert.Equal(expectedName, source.Name);
    }
}
