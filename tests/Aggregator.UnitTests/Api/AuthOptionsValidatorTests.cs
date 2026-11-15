using Aggregator.Api.Auth;
using Microsoft.Extensions.Options;

namespace Aggregator.UnitTests.Api;

[Requirement("O1")]
public sealed class AuthOptionsValidatorTests
{
    [Fact]
    public void Validate_MockEnabled_DoesNotNeedAuthority() =>
        Assert.True(Validate(mockEnabled: true, authority: null).Succeeded);

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [FailureScenario]
    public void Validate_MockDisabledWithoutAuthority_Fails(string? authority)
    {
        var result = Validate(mockEnabled: false, authority);

        Assert.True(result.Failed);
        Assert.Contains("Auth:Authority is required", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://login.example.com/tenant", false)]
    [InlineData("not a url", false)]
    [InlineData("https://login.microsoftonline.com/tenant/v2.0", true)]
    [FailureScenario]
    public void Validate_MockDisabled_RequiresAbsoluteHttpsAuthority(string authority, bool valid) =>
        Assert.Equal(valid, Validate(mockEnabled: false, authority).Succeeded);

    private static ValidateOptionsResult Validate(bool mockEnabled, string? authority) =>
        new AuthOptionsValidator(Options.Create(new MockAuthOptions { Enabled = mockEnabled }))
            .Validate(null, new AuthOptions { Issuer = "issuer", Audience = "audience", RequiredRole = "reader", Authority = authority });
}
