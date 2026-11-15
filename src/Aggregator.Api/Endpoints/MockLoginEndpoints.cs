using System.Collections.Frozen;
using Aggregator.Api.Auth;
using Aggregator.Api.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Aggregator.Api.Endpoints;

internal static class MockLoginEndpoints
{
    private static readonly FrozenDictionary<string, MockUser> Users = new Dictionary<string, MockUser>
    {
        ["valid"] = MockUser.Valid,
        ["invalid"] = MockUser.Invalid,
        ["forbidden"] = MockUser.Forbidden
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static IEndpointRouteBuilder MapMockLogin(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/mock-login", Login)
            .AllowAnonymous()
            .WithTags("Auth")
            .WithSummary("Issues a short-lived test token (mock, enabled by configuration).")
            .WithDescription("user: 'valid' (200 on protected endpoints), 'invalid' (signed with an untrusted key → 401), 'forbidden' (missing the required role → 403).");
        return endpoints;
    }

    private static Results<Ok<TokenResponse>, ValidationProblem> Login(MockLoginRequest request, MockTokenIssuer issuer)
    {
        if (request.User is null || !Users.TryGetValue(request.User, out var user))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["user"] = ["Allowed values: valid, invalid, forbidden."]
            });
        }

        var token = issuer.Issue(user);
        return TypedResults.Ok(new TokenResponse(token.AccessToken, "Bearer", token.ExpiresInSeconds));
    }
}
