using Aggregator.Api.Auth;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Aggregator.Api.Composition;

internal sealed class BearerSecurityTransformer(IOptions<MockAuthOptions> mockOptions) : IOpenApiDocumentTransformer
{
    public const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = mockOptions.Value.Enabled
                ? "A JWT bearer token. Get a test token from POST /mock-login."
                : "A JWT bearer token from the configured identity provider."
        };

        return Task.CompletedTask;
    }
}
