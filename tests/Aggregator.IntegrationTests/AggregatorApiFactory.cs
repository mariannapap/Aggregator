using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Aggregator.IntegrationTests;

public sealed class AggregatorApiFactory : WebApplicationFactory<Program>
{
    // Every client gets one retry, so a failing call reaches the API this many times.
    public const int AttemptsPerCall = 2;

    public OpenMeteoStub OpenMeteo { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("MockAuth:Enabled", "true");
        builder.UseSetting("OpenApi:EnableUi", "false");
        foreach (var client in new[] { "Geocoding", "Weather", "AirQuality", "Marine", "Hazards" })
        {
            builder.UseSetting($"HttpClients:{client}:Resilience:Retry:Delay", "00:00:00.001");
            builder.UseSetting($"HttpClients:{client}:Resilience:Retry:MaxRetryAttempts", (AttemptsPerCall - 1).ToString(CultureInfo.InvariantCulture));
        }

        builder.ConfigureTestServices(services =>
            services.ConfigureAll<HttpClientFactoryOptions>(options =>
                options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = OpenMeteo)));
    }
}
