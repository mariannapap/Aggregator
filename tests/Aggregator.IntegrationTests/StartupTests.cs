using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Infrastructure.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace Aggregator.IntegrationTests;

public sealed class StartupTests
{
    [Theory]
    [InlineData("Statistics:FastBelowMs", "300", "FastBelowMs")]
    [InlineData("Anomaly:WindowMinutes", "30", "RecentMinutesKept")]
    [InlineData("CityReport:HourlyWindowHours", "0", "HourlyWindowHours")]
    [InlineData("HttpClients:Weather:BaseUrl", "", "BaseUrl")]
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
