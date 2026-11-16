using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
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

    [Fact]
    public async Task EntraShapedToken_WithARolesArray_Returns200()
    {
        await using var factory = new AggregatorApiFactory();
        var reader = factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value.RequiredRole;

        var token = CreateToken(factory, roleClaims: new Dictionary<string, object> { ["roles"] = new[] { "other", reader } });
        var response = await GetCityReportAsync(factory, new AuthenticationHeaderValue("Bearer", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [FailureScenario]
    public async Task TokenWithTheSingularRoleClaim_Returns403()
    {
        await using var factory = new AggregatorApiFactory();
        var reader = factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value.RequiredRole;

        var token = CreateToken(factory, roleClaims: new Dictionary<string, object> { ["role"] = reader });
        var response = await GetCityReportAsync(factory, new AuthenticationHeaderValue("Bearer", token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MockToken_CarriesTheRoleInARolesArray_LikeEntra()
    {
        await using var factory = new AggregatorApiFactory();
        var login = await factory.CreateClient().PostAsJsonAsync(new Uri("/mock-login", UriKind.Relative), new { user = "valid" }, Ct);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("access_token").GetString()!;

        var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(new JsonWebToken(token).EncodedPayload)).RootElement;

        Assert.Equal(JsonValueKind.Array, payload.GetProperty("roles").ValueKind);
        Assert.Equal(["reader"], payload.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        Assert.False(payload.TryGetProperty("role", out _));
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

    private static string CreateToken(
        AggregatorApiFactory factory,
        string? issuer = null,
        string? audience = null,
        DateTime? expires = null,
        Dictionary<string, object>? roleClaims = null)
    {
        var auth = factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value;
        var key = factory.Services.GetRequiredService<MockSigningKeys>().Trusted;
        var expiry = expires ?? DateTime.UtcNow.AddMinutes(2);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer ?? auth.Issuer,
            Audience = audience ?? auth.Audience,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, "test")]),
            Claims = roleClaims ?? new Dictionary<string, object> { [AuthClaims.Roles] = auth.RequiredRole },
            IssuedAt = expiry.AddMinutes(-10),
            NotBefore = expiry.AddMinutes(-10),
            Expires = expiry,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        });
    }
}
