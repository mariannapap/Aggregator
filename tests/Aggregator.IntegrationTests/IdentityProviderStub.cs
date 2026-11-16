using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Aggregator.IntegrationTests;

// Serves OpenID Connect discovery and the signing keys the way Entra ID does, so JwtBearer runs its real Authority path.
public sealed class IdentityProviderStub : HttpMessageHandler
{
    public const string Authority = "https://login.test/tenant-id/v2.0";
    private const string KeysUri = "https://login.test/tenant-id/discovery/v2.0/keys";

    private readonly RSA rsa = RSA.Create(2048);

    public IdentityProviderStub() => SigningKey = new RsaSecurityKey(rsa) { KeyId = "provider-key" };

    public RsaSecurityKey SigningKey { get; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.RequestUri!.AbsoluteUri switch
        {
            $"{Authority}/.well-known/openid-configuration" => JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["issuer"] = Authority,
                ["jwks_uri"] = KeysUri,
                ["id_token_signing_alg_values_supported"] = new[] { SecurityAlgorithms.RsaSha256 }
            }),
            KeysUri => Keys(),
            _ => null
        };

        return Task.FromResult(body is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            rsa.Dispose();
        base.Dispose(disposing);
    }

    private string Keys()
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        return JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new { kty = "RSA", use = "sig", kid = SigningKey.KeyId, n = Base64UrlEncoder.Encode(parameters.Modulus), e = Base64UrlEncoder.Encode(parameters.Exponent) }
            }
        });
    }
}
