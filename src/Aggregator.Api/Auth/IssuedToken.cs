namespace Aggregator.Api.Auth;

internal sealed record IssuedToken(string AccessToken, int ExpiresInSeconds);
