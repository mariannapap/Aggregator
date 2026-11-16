using System.Text.Json.Serialization;

namespace Aggregator.Api.Contracts;

/// <summary>A token from the mock issuer, shaped like an OAuth 2.0 token response.</summary>
/// <param name="AccessToken">The JWT to send as "Authorization: Bearer {token}".</param>
/// <param name="TokenType">Always Bearer.</param>
/// <param name="ExpiresIn">Seconds until the token expires.</param>
public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
