using System.Net.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Mozaiks.Auth.Authorization;

namespace Mozaiks.Auth;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddMozaiksAuth(
        this WebApplicationBuilder builder,
        string? signalRHubPathPrefix = null)
    {
        var options = MozaiksAuthOptions.FromEnvironment();

        ValidateConfiguration(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IUserContextAccessor, UserContextAccessor>();

        builder.Services
            .AddAuthentication(authenticationOptions =>
            {
                authenticationOptions.DefaultAuthenticateScheme = MozaiksAuthDefaults.UserScheme;
                authenticationOptions.DefaultChallengeScheme = MozaiksAuthDefaults.UserScheme;
            })
            .AddJwtBearer(MozaiksAuthDefaults.UserScheme, jwtOptions =>
            {
                jwtOptions.MapInboundClaims = false;
                jwtOptions.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                jwtOptions.SaveToken = true;

                if (string.Equals(options.Provider, "jwt", StringComparison.OrdinalIgnoreCase))
                {
                    var issuer = options.Issuer ?? string.Empty;
                    var jwksUrl = options.JwksUrl ?? string.Empty;

                    jwtOptions.ConfigurationManager = new JwksOpenIdConnectConfigurationManager(
                        new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
                        issuer,
                        jwksUrl);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(options.MetadataAddress))
                    {
                        jwtOptions.MetadataAddress = options.MetadataAddress;
                    }
                    else if (!string.IsNullOrWhiteSpace(options.Authority))
                    {
                        jwtOptions.Authority = ResolveAuthority(options);
                    }
                }

                jwtOptions.Audience = options.Audience;

                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    RoleClaimType = options.RolesClaim,
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    ValidIssuer = !string.IsNullOrWhiteSpace(options.Issuer) ? options.Issuer : null
                };

                jwtOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (!string.IsNullOrWhiteSpace(signalRHubPathPrefix))
                        {
                            var accessToken = context.Request.Query["access_token"].ToString();
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrWhiteSpace(accessToken)
                                && path.StartsWithSegments(signalRHubPathPrefix, StringComparison.OrdinalIgnoreCase))
                            {
                                context.Token = accessToken;
                            }
                        }

                        return Task.CompletedTask;
                    },

                    OnTokenValidated = context =>
                    {
                        var principal = context.Principal;
                        if (principal is null)
                        {
                            context.Fail("Missing principal.");
                            return Task.CompletedTask;
                        }

                        var scopes = GetScopes(principal);
                        if (!scopes.Contains(options.RequiredScope, StringComparer.OrdinalIgnoreCase))
                        {
                            context.Fail($"Missing required scope '{options.RequiredScope}'.");
                            return Task.CompletedTask;
                        }

                        if (options.AllowedClientIds.Length > 0)
                        {
                            var clientId = GetClientId(principal);
                            if (string.IsNullOrWhiteSpace(clientId)
                                || !options.AllowedClientIds.Contains(clientId, StringComparer.OrdinalIgnoreCase))
                            {
                                context.Fail("Token client application is not allowed.");
                                return Task.CompletedTask;
                            }
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthentication().AddJwtBearer(MozaiksAuthDefaults.InternalScheme, jwtOptions =>
        {
            jwtOptions.MapInboundClaims = false;
            jwtOptions.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            jwtOptions.SaveToken = false;

            if (string.Equals(options.Provider, "jwt", StringComparison.OrdinalIgnoreCase))
            {
                var issuer = options.Issuer ?? string.Empty;
                var jwksUrl = options.JwksUrl ?? string.Empty;

                jwtOptions.ConfigurationManager = new JwksOpenIdConnectConfigurationManager(
                    new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
                    issuer,
                    jwksUrl);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(options.MetadataAddress))
                {
                    jwtOptions.MetadataAddress = options.MetadataAddress;
                }
                else if (!string.IsNullOrWhiteSpace(options.Authority))
                {
                    jwtOptions.Authority = ResolveAuthority(options);
                }
            }

            jwtOptions.Audience = options.Audience;

            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                RoleClaimType = options.RolesClaim,
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
                ValidIssuer = !string.IsNullOrWhiteSpace(options.Issuer) ? options.Issuer : null
            };

            jwtOptions.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var principal = context.Principal;
                    if (principal is null)
                    {
                        context.Fail("Missing principal.");
                        return Task.CompletedTask;
                    }

                    if (GetScopes(principal).Count > 0)
                    {
                        context.Fail("Internal service tokens must not be delegated user tokens.");
                        return Task.CompletedTask;
                    }

                    if (options.AllowedClientIds.Length > 0)
                    {
                        var clientId = GetClientId(principal);
                        if (string.IsNullOrWhiteSpace(clientId)
                            || !options.AllowedClientIds.Contains(clientId, StringComparer.OrdinalIgnoreCase))
                        {
                            context.Fail("Token client application is not allowed.");
                            return Task.CompletedTask;
                        }
                    }

                    return Task.CompletedTask;
                }
            };
        });

        builder.Services.AddAuthorization(authorizationOptions =>
        {
            authorizationOptions.AddPolicy(
                MozaiksAuthDefaults.RequirePlatformAdminPolicy,
                policy => policy.Requirements.Add(new PlatformAdminRequirement()));

            authorizationOptions.AddPolicy(
                MozaiksAuthDefaults.RequireSuperAdminPolicy,
                policy => policy.Requirements.Add(new SuperAdminRequirement()));

            authorizationOptions.AddPolicy(
                MozaiksAuthDefaults.RequireMfaPolicy,
                policy => policy.Requirements.Add(new MfaRequirement()));

            authorizationOptions.AddPolicy(
                MozaiksAuthDefaults.InternalServicePolicy,
                policy =>
                {
                    policy.AuthenticationSchemes.Add(MozaiksAuthDefaults.InternalScheme);
                    policy.RequireAuthenticatedUser();
                    policy.RequireAssertion(context =>
                    {
                        return GetScopes(context.User).Count == 0;
                    });
                });
        });

        builder.Services.AddSingleton<IAuthorizationHandler, PlatformAdminHandler>();
        builder.Services.AddSingleton<IAuthorizationHandler, SuperAdminHandler>();
        builder.Services.AddSingleton<IAuthorizationHandler, MfaHandler>();

        return builder;
    }

    private static string ResolveAuthority(MozaiksAuthOptions options)
    {
        var authority = NormalizeAuthority(options.Authority ?? string.Empty);
        if (string.IsNullOrWhiteSpace(authority))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri))
        {
            return authority;
        }

        if (!IsCiamAuthority(uri))
        {
            return authority;
        }

        if (IsRootPath(uri))
        {
            var tenantId = (options.TenantId ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                authority = $"{authority}/{tenantId}";
            }
        }

        if (!authority.EndsWith("/v2.0", StringComparison.OrdinalIgnoreCase))
        {
            authority = $"{authority}/v2.0";
        }

        return authority;
    }

    private static string NormalizeAuthority(string authority)
        => authority.Trim().TrimEnd('/');

    private static bool IsCiamAuthority(Uri authority)
        => authority.Host.EndsWith(".ciamlogin.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsRootPath(Uri authority)
        => string.IsNullOrWhiteSpace(authority.AbsolutePath) || string.Equals(authority.AbsolutePath, "/", StringComparison.Ordinal);

    private static void ValidateConfiguration(MozaiksAuthOptions options)
    {
        var provider = (options.Provider ?? string.Empty).Trim();
        var hasAuthority = !string.IsNullOrWhiteSpace(options.Authority);
        var hasMetadata = !string.IsNullOrWhiteSpace(options.MetadataAddress);
        var hasIssuer = !string.IsNullOrWhiteSpace(options.Issuer);
        var hasJwks = !string.IsNullOrWhiteSpace(options.JwksUrl);

        if (string.Equals(provider, "jwt", StringComparison.OrdinalIgnoreCase))
        {
            if (!hasIssuer)
            {
                throw new InvalidOperationException("AUTH_ISSUER must be configured when AUTH_PROVIDER=jwt.");
            }
            if (!hasJwks)
            {
                throw new InvalidOperationException("AUTH_JWKS_URL must be configured when AUTH_PROVIDER=jwt.");
            }
        }
        else if (string.Equals(provider, "ciam", StringComparison.OrdinalIgnoreCase))
        {
            if (!hasAuthority && !hasMetadata)
            {
                throw new InvalidOperationException("AUTH_AUTHORITY or AUTH_METADATA_ADDRESS must be configured.");
            }

            if (hasAuthority && !hasMetadata
                && Uri.TryCreate(NormalizeAuthority(options.Authority!), UriKind.Absolute, out var authority)
                && IsCiamAuthority(authority)
                && IsRootPath(authority)
                && string.IsNullOrWhiteSpace(options.TenantId))
            {
                throw new InvalidOperationException("AUTH_TENANT_ID must be configured when AUTH_AUTHORITY is a CIAM root (https://*.ciamlogin.com/).");
            }
        }
        else
        {
            throw new InvalidOperationException("AUTH_PROVIDER must be set to 'ciam' or 'jwt'.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("AUTH_AUDIENCE must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.RequiredScope))
        {
            throw new InvalidOperationException("AUTH_REQUIRED_SCOPE must be configured.");
        }
    }

    private static string? GetClientId(ClaimsPrincipal principal)
    {
        return principal.Claims.FirstOrDefault(c => string.Equals(c.Type, "azp", StringComparison.OrdinalIgnoreCase))?.Value
               ?? principal.Claims.FirstOrDefault(c => string.Equals(c.Type, "appid", StringComparison.OrdinalIgnoreCase))?.Value
               ?? principal.Claims.FirstOrDefault(c => string.Equals(c.Type, "client_id", StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static IReadOnlyCollection<string> GetScopes(ClaimsPrincipal principal)
    {
        var raw = principal.Claims.FirstOrDefault(c => string.Equals(c.Type, "scp", StringComparison.OrdinalIgnoreCase))?.Value
                  ?? principal.Claims.FirstOrDefault(c => string.Equals(c.Type, "scope", StringComparison.OrdinalIgnoreCase))?.Value
                  ?? string.Empty;

        return raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
