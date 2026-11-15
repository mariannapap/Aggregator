using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Aggregator.Api.Auth;

internal sealed class MockSigningKeys : IDisposable
{
    private readonly RSA trusted = RSA.Create(2048);
    private readonly RSA untrusted = RSA.Create(2048);

    public MockSigningKeys()
    {
        Trusted = new RsaSecurityKey(trusted) { KeyId = "mock-trusted" };
        Untrusted = new RsaSecurityKey(untrusted) { KeyId = "mock-untrusted" };
    }

    public RsaSecurityKey Trusted { get; }

    public RsaSecurityKey Untrusted { get; }

    public void Dispose()
    {
        trusted.Dispose();
        untrusted.Dispose();
    }
}
