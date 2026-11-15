using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Aggregator.Api.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Aggregator.IntegrationTests;

[Requirement("O1")]
public sealed class AuthTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [FailureScenario]
    public async Task ExpiredToken_Returns401WithExpiryReason()
    {
        await using var factory = new AggregatorApiFactory();
        var token = CreateToken(factory, expires: DateTime.UtcNow.AddMinutes(-5));

        var response = await GetCityReportAsync(factory, new AuthenticationHeaderValue("Bearer", token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("expired", response.Headers.WwwAuthenticate.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("wrong-issuer", null)]
    [InlineData(null, "wrong-audience")]
    [FailureScenario]
    public async Task TokenForAnotherIssuerOrAudience_Returns401(string? issuer, string? audience)
    {
        await using var factory = new AggregatorApiFactory();
        var token = CreateToken(factory, issuer: issuer, audience: audience);

        var response = await GetCityReportAsync(factory, new AuthenticationHeaderValue("Bearer", token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidHandMadeToken_Returns200()
    {
        await using var factory = new AggregatorApiFactory();

        var response = await GetCityReportAsync(factory, new AuthenticationHeaderValue("Bearer", CreateToken(factory)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Bearer", "not-a-jwt")]
    [InlineData("Bearer", "eyJhbGciOiJub25lIn0.eyJzdWIiOiJ4In0.")]
    [InlineData("Basic", "dXNlcjpwYXNzd29yZA==")]
    [FailureScenario]
    public async Task MalformedTokenOrWrongScheme_Returns401(string scheme, string value)
    {
        await using var factory = new AggregatorApiFactory();

        var response = await GetCityReportAsync(factory, new AuthenticationHeaderValue(scheme, value));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetCityReportAsync(AggregatorApiFactory factory, AuthenticationHeaderValue authorization)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = authorization;
        return await client.GetAsync(new Uri("/city-report/GR/Athens", UriKind.Relative), Ct);
    }

    private static string CreateToken(AggregatorApiFactory factory, string? issuer = null, string? audience = null, DateTime? expires = null)
    {
        var auth = factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value;
        var key = factory.Services.GetRequiredService<MockSigningKeys>().Trusted;
        var expiry = expires ?? DateTime.UtcNow.AddMinutes(2);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer ?? auth.Issuer,
            Audience = audience ?? auth.Audience,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, "test"), new Claim(AuthClaims.Role, auth.RequiredRole)]),
            IssuedAt = expiry.AddMinutes(-10),
            NotBefore = expiry.AddMinutes(-10),
            Expires = expiry,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        });
    }
}
