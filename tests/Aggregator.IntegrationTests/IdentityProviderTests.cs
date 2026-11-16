using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Aggregator.Api.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Aggregator.IntegrationTests;

// The production path: mock issuer off, keys discovered from Auth:Authority, Entra-shaped tokens.
[Requirement("O1")]
public sealed class IdentityProviderTests : IDisposable
{
    private const string Audience = "api://aggregator";
    private const string Role = "Reports.Read";

    private readonly IdentityProviderStub provider = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EntraShapedToken_FromTheConfiguredAuthority_Returns200()
    {
        await using var factory = new AggregatorApiFactory();

        var response = await GetCityReportAsync(WithProvider(factory), CreateToken(provider.SigningKey));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [FailureScenario]
    public async Task TokenForAnotherAudience_Returns401()
    {
        await using var factory = new AggregatorApiFactory();

        var response = await GetCityReportAsync(WithProvider(factory), CreateToken(provider.SigningKey, audience: "api://another-app"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [FailureScenario]
    public async Task TokenSignedWithTheMockKey_Returns401_AndMockLoginIsNotMapped()
    {
        await using var factory = new AggregatorApiFactory();
        var app = WithProvider(factory);

        var response = await GetCityReportAsync(app, CreateToken(app.Services.GetRequiredService<MockSigningKeys>().Trusted));
        var login = await app.CreateClient().PostAsJsonAsync(new Uri("/mock-login", UriKind.Relative), new { user = "valid" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // Not mapped; the fallback policy answers 401, not 404, to anonymous callers of any unmapped path.
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    [FailureScenario]
    public async Task TokenSignedWithAnotherAlgorithm_Returns401()
    {
        await using var factory = new AggregatorApiFactory();

        var response = await GetCityReportAsync(WithProvider(factory), CreateToken(provider.SigningKey, algorithm: SecurityAlgorithms.RsaSha512));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose() => provider.Dispose();

    private WebApplicationFactory<Program> WithProvider(AggregatorApiFactory factory)
    {
        factory.OpenMeteo.GeneratedPlaces = 1;
        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MockAuth:Enabled", "false");
            builder.UseSetting("Auth:Authority", IdentityProviderStub.Authority);
            builder.UseSetting("Auth:Issuer", IdentityProviderStub.Authority);
            builder.UseSetting("Auth:Audience", Audience);
            builder.UseSetting("Auth:RequiredRole", Role);

            // Configure, not PostConfigure: JwtBearer builds its metadata client from this handler in its own post-configuration.
            builder.ConfigureTestServices(services =>
                services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwt => jwt.BackchannelHttpHandler = provider));
        });
    }

    private static async Task<HttpResponseMessage> GetCityReportAsync(WebApplicationFactory<Program> app, string token)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync(new Uri("/city-report/GR/Anywhere", UriKind.Relative), Ct);
    }

    // Shaped like an Entra ID v2.0 access token for an app with an app role.
    private static string CreateToken(SecurityKey key, string audience = Audience, string algorithm = SecurityAlgorithms.RsaSha256) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = IdentityProviderStub.Authority,
            Audience = audience,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, "subject-id")]),
            Claims = new Dictionary<string, object>
            {
                [AuthClaims.Roles] = new[] { Role },
                ["tid"] = "tenant-id",
                ["oid"] = "object-id",
                ["azp"] = "client-app-id",
                ["ver"] = "2.0"
            },
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, algorithm)
        });
}
