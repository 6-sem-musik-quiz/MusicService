using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace MusicService.Api.Auth;

public static class JwtAuthenticationExtensions
{
    // Validates the JWT that UserService issues (same rules as SCRUM-88): HS256 with Jwt:SigningKey,
    // issuer slopster-userservice, audience slopster. Every endpoint needs a valid token
    // (fallback policy); admin endpoints also need the AdminOnly policy.
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration>((options, config) =>
            {
                var signingKey = config["Jwt:SigningKey"];
                options.MapInboundClaims = false; // keep the claim names UserService writes: sub, unique_name, role
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = config["Jwt:Issuer"] ?? "slopster-userservice",
                    ValidateAudience = true,
                    ValidAudience = config["Jwt:Audience"] ?? "slopster",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    // Without a configured key no token validates, so every protected endpoint returns 401.
                    IssuerSigningKey = string.IsNullOrEmpty(signingKey)
                        ? null
                        : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    NameClaimType = "unique_name",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy(AuthPolicies.AdminOnly, policy => policy.RequireRole("Admin"));
        });

        return services;
    }
}
