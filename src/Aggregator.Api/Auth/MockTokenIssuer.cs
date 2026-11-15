using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Aggregator.Api.Auth;

internal sealed class MockTokenIssuer(
    MockSigningKeys keys,
    IOptions<AuthOptions> authOptions,
    IOptions<MockAuthOptions> mockOptions,
    TimeProvider timeProvider)
{
    private readonly JsonWebTokenHandler handler = new();

    public IssuedToken Issue(MockUser user)
    {
        var auth = authOptions.Value;
        var lifetime = mockOptions.Value.Lifetime;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, $"mock-{user.ToString().ToLowerInvariant()}"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        if (user != MockUser.Forbidden)
            claims.Add(new Claim(AuthClaims.Role, auth.RequiredRole));

        var signingKey = user == MockUser.Invalid ? keys.Untrusted : keys.Trusted;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = auth.Issuer,
            Audience = auth.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(lifetime),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
        };

        return new IssuedToken(handler.CreateToken(descriptor), (int)lifetime.TotalSeconds);
    }
}
