using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Aggregator.IntegrationTests;

public sealed class AggregatorApiFactory : WebApplicationFactory<Program>
{
    public OpenMeteoStub OpenMeteo { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("MockAuth:Enabled", "true");
        builder.UseSetting("OpenApi:EnableUi", "false");
        foreach (var client in new[] { "Geocoding", "Weather", "AirQuality" })
        {
            builder.UseSetting($"HttpClients:{client}:Resilience:Retry:Delay", "00:00:00.001");
            builder.UseSetting($"HttpClients:{client}:Resilience:Retry:MaxRetryAttempts", "1");
        }

        builder.ConfigureTestServices(services =>
            services.ConfigureAll<HttpClientFactoryOptions>(options =>
                options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = OpenMeteo)));
    }
}
