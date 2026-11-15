using Microsoft.Extensions.Options;

namespace Aggregator.Api.Auth;

internal sealed class AuthOptionsValidator(IOptions<MockAuthOptions> mockOptions) : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        if (mockOptions.Value.Enabled)
            return ValidateOptionsResult.Success;

        if (string.IsNullOrWhiteSpace(options.Authority))
            return ValidateOptionsResult.Fail("Auth:Authority is required when MockAuth:Enabled is false: without it no token can be validated.");

        return Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority) && authority.Scheme == Uri.UriSchemeHttps
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Auth:Authority must be an absolute https URL.");
    }
}
