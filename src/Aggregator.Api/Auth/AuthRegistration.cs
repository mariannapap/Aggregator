using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Aggregator.Api.Auth;

internal static class AuthRegistration
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<MockAuthOptions>().BindConfiguration(MockAuthOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();
        services.AddSingleton<MockSigningKeys>();
        services.AddSingleton<MockTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>, IOptions<MockAuthOptions>, MockSigningKeys>((jwt, auth, mock, keys) =>
            {
                var settings = auth.Value;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.ValidateIssuer = true;
                jwt.TokenValidationParameters.ValidIssuer = settings.Issuer;
                jwt.TokenValidationParameters.ValidateAudience = true;
                jwt.TokenValidationParameters.ValidAudience = settings.Audience;
                jwt.TokenValidationParameters.ValidateLifetime = true;
                jwt.TokenValidationParameters.ValidateIssuerSigningKey = true;
                jwt.TokenValidationParameters.ClockSkew = settings.ClockSkew;
                jwt.TokenValidationParameters.RoleClaimType = AuthClaims.Roles;
                jwt.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.Sub;

                if (mock.Value.Enabled)
                    jwt.TokenValidationParameters.IssuerSigningKey = keys.Trusted;
                else
                    jwt.Authority = settings.Authority;
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddOptions<AuthorizationOptions>()
            .Configure<IOptions<AuthOptions>>((authorization, auth) =>
                authorization.AddPolicy(AuthOptions.ReaderPolicy, policy => policy.RequireAuthenticatedUser().RequireRole(auth.Value.RequiredRole)));

        return services;
    }
}
