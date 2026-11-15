using System.Net;
using System.Text;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Aggregator.IntegrationTests;

[Requirement("R6")]
public sealed class ErrorHandlingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [FailureScenario]
    public async Task MalformedRequestBody_Returns400ProblemDetails()
    {
        await using var factory = new AggregatorApiFactory();

        var response = await PostMalformedLoginAsync(factory.CreateClient());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    [FailureScenario]
    public async Task MalformedRequestBody_WhenBindingThrows_IsStill400NotA500()
    {
        await using var factory = new AggregatorApiFactory();
        var client = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true))).CreateClient();

        var response = await PostMalformedLoginAsync(client);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    [FailureScenario]
    public async Task UnexpectedException_Returns500ProblemDetailsWithoutInternals()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<HybridCache, ThrowingHybridCache>())).CreateAuthorizedClientAsync();

        var response = await client.GetAsync(new Uri("/city-report/GR/Athens", UriKind.Relative), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("secret detail", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain(" at Aggregator", body, StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> PostMalformedLoginAsync(HttpClient client) =>
        client.PostAsync(
            new Uri("/mock-login", UriKind.Relative),
            new StringContent("{ not json", Encoding.UTF8, "application/json"),
            Ct);
}
