using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Mozaiks.Auth;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Mozaiks.Auth.Tests;

public sealed class MozaiksAuthOidcTests
{
    [Fact]
    public async Task BearerToken_Validates_via_oidc_metadata_and_jwks()
    {
        await using var oidc = await FakeOidcServer.StartAsync();
        await using var api = await TestApi.StartAsync(oidc);

        var token = oidc.CreateAccessToken(
            audience: TestApi.TestAudience,
            scopeClaimType: "scp",
            scopeValue: "access_as_user",
            roles: ["User"]);

        var response = await api.GetAsync("/whoami", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(oidc.MetadataRequests > 0, "Expected OIDC discovery document to be fetched");
        Assert.True(oidc.JwksRequests > 0, "Expected JWKS to be fetched");

        var who = await response.Content.ReadFromJsonAsync<WhoAmIResponse>();
        Assert.NotNull(who);
        Assert.Equal("user-123", who!.UserId);
        Assert.Equal("user@example.com", who.Email);
        Assert.Contains("User", who.Roles);
        Assert.Equal("tenant-123", who.TenantId);
        Assert.False(who.IsSuperAdmin);
    }

    [Fact]
    public async Task BearerToken_Validates_via_jwks_when_provider_is_jwt()
    {
        await using var oidc = await FakeOidcServer.StartAsync();
        await using var api = await TestApi.StartAsync(oidc, new Dictionary<string, string?>
        {
            ["AUTH_PROVIDER"] = "jwt",
            ["AUTH_ISSUER"] = oidc.Issuer,
            ["AUTH_JWKS_URL"] = oidc.JwksUri
        });

        var token = oidc.CreateAccessToken(
            audience: TestApi.TestAudience,
            scopeClaimType: "scp",
            scopeValue: "access_as_user");

        var response = await api.GetAsync("/whoami", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(oidc.MetadataRequests == 0, "Expected OIDC discovery document NOT to be fetched when AUTH_PROVIDER=jwt");
        Assert.True(oidc.JwksRequests > 0, "Expected JWKS to be fetched");
    }

    [Fact]
    public async Task BearerToken_Validates_via_authority_when_provider_is_ciam()
    {
        await using var oidc = await FakeOidcServer.StartAsync();
        await using var api = await TestApi.StartAsync(oidc, new Dictionary<string, string?>
        {
            ["AUTH_METADATA_ADDRESS"] = ""
        });

        var token = oidc.CreateAccessToken(
            audience: TestApi.TestAudience,
            scopeClaimType: "scp",
            scopeValue: "access_as_user");

        var response = await api.GetAsync("/whoami", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(oidc.MetadataRequests > 0, "Expected OIDC discovery document to be fetched");
        Assert.True(oidc.JwksRequests > 0, "Expected JWKS to be fetched");
    }

    [Fact]
    public async Task BearerToken_Accepts_scope_claim_variant()
    {
        await using var oidc = await FakeOidcServer.StartAsync();
        await using var api = await TestApi.StartAsync(oidc);

        var token = oidc.CreateAccessToken(
            audience: TestApi.TestAudience,
            scopeClaimType: "scope",
            scopeValue: "access_as_user something_else");

        var response = await api.GetAsync("/whoami", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BearerToken_Missing_required_scope_is_rejected()
    {
        await using var oidc = await FakeOidcServer.StartAsync();
        await using var api = await TestApi.StartAsync(oidc);

        var token = oidc.CreateAccessToken(
            audience: TestApi.TestAudience,
            scopeClaimType: "scp",
            scopeValue: "some_other_scope");

        var response = await api.GetAsync("/whoami", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RequireSuperAdminPolicy_Allows_role()
    {
        await using var oidc = await FakeOidcServer.StartAsync();
        await using var api = await TestApi.StartAsync(oidc);

        var token = oidc.CreateAccessToken(
            audience: TestApi.TestAudience,
            scopeClaimType: "scp",
            scopeValue: "access_as_user",
            roles: ["SuperAdmin"]);

        var response = await api.GetAsync("/superadmin", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RequireSuperAdminPolicy_Denies_when_missing_role()
    {
        await using var oidc = await FakeOidcServer.StartAsync();
        await using var api = await TestApi.StartAsync(oidc);

        var token = oidc.CreateAccessToken(
            audience: TestApi.TestAudience,
            scopeClaimType: "scp",
            scopeValue: "access_as_user",
            roles: ["User"]);

        var response = await api.GetAsync("/superadmin", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RequirePlatformAdminPolicy_Allows_admin_role()
    {
        await using var oidc = await FakeOidcServer.StartAsync();
        await using var api = await TestApi.StartAsync(oidc);

        var token = oidc.CreateAccessToken(
            audience: TestApi.TestAudience,
            scopeClaimType: "scp",
            scopeValue: "access_as_user",
            roles: ["Admin"]);

        var response = await api.GetAsync("/platformadmin", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RequirePlatformAdminPolicy_Denies_when_missing_role()
    {
        await using var oidc = await FakeOidcServer.StartAsync();
        await using var api = await TestApi.StartAsync(oidc);

        var token = oidc.CreateAccessToken(
            audience: TestApi.TestAudience,
            scopeClaimType: "scp",
            scopeValue: "access_as_user",
            roles: ["User"]);

        var response = await api.GetAsync("/platformadmin", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InternalServicePolicy_Allows_app_only_token_with_roles()
    {
        await using var oidc = await FakeOidcServer.StartAsync();
        await using var api = await TestApi.StartAsync(oidc);

        var token = oidc.CreateAccessToken(
            audience: TestApi.TestAudience,
            scopeClaimType: null,
            scopeValue: null,
            roles: ["Internal.App"]);

        var response = await api.GetAsync("/internal", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InternalServicePolicy_Rejects_delegated_user_token()
    {
        await using var oidc = await FakeOidcServer.StartAsync();
        await using var api = await TestApi.StartAsync(oidc);

        var token = oidc.CreateAccessToken(
            audience: TestApi.TestAudience,
            scopeClaimType: "scp",
            scopeValue: "access_as_user",
            roles: ["Internal.App"]);

        var response = await api.GetAsync("/internal", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record WhoAmIResponse(
        string UserId,
        string Email,
        string DisplayName,
        string[] Roles,
        string TenantId,
        bool IsSuperAdmin);

    private sealed class TestApi : IAsyncDisposable
    {
        public const string TestAudience = "api://test-api";

        private readonly WebApplication _app;
        private readonly EnvVarScope _env;

        public HttpClient Client { get; }

        private TestApi(WebApplication app, EnvVarScope env)
        {
            _app = app;
            _env = env;
            Client = app.GetTestClient();
        }

        public static async Task<TestApi> StartAsync(FakeOidcServer oidc, IReadOnlyDictionary<string, string?>? overrides = null)
        {
            var envValues = new Dictionary<string, string?>
            {
                ["AUTH_PROVIDER"] = "ciam",
                ["AUTH_AUTHORITY"] = oidc.Issuer,
                ["AUTH_TENANT_ID"] = null,
                ["AUTH_METADATA_ADDRESS"] = oidc.MetadataAddress,
                ["AUTH_AUDIENCE"] = TestAudience,
                ["AUTH_REQUIRED_SCOPE"] = "access_as_user",
                ["AUTH_USER_ID_CLAIM"] = "sub",
                ["AUTH_EMAIL_CLAIM"] = "email",
                ["AUTH_ROLES_CLAIM"] = "roles",
                ["AUTH_TENANT_CLAIM"] = "tid",
                ["AUTH_SUPERADMIN_ROLE"] = "SuperAdmin",
                ["AUTH_ALLOWED_CLIENT_IDS"] = null,
                ["AUTH_ISSUER"] = null,
                ["AUTH_JWKS_URL"] = null
            };

            if (overrides is not null)
            {
                foreach (var (key, value) in overrides)
                {
                    envValues[key] = value;
                }
            }

            var env = new EnvVarScope(envValues);

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            builder.WebHost.UseTestServer();
            builder.AddMozaiksAuth();

            var app = builder.Build();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGet("/whoami", (IUserContextAccessor userContextAccessor) =>
            {
                var user = userContextAccessor.GetRequiredUser();
                return Results.Json(new
                {
                    userId = user.UserId,
                    email = user.Email,
                    displayName = user.DisplayName,
                    roles = user.Roles.ToArray(),
                    tenantId = user.TenantId,
                    isSuperAdmin = user.IsSuperAdmin
                });
            }).RequireAuthorization();

            app.MapGet("/superadmin", () => Results.Ok())
                .RequireAuthorization(MozaiksAuthDefaults.RequireSuperAdminPolicy);

            app.MapGet("/platformadmin", () => Results.Ok())
                .RequireAuthorization(MozaiksAuthDefaults.RequirePlatformAdminPolicy);

            app.MapGet("/internal", () => Results.Ok())
                .RequireAuthorization(MozaiksAuthDefaults.InternalServicePolicy);

            await app.StartAsync();

            return new TestApi(app, env);
        }

        public async Task<HttpResponseMessage> GetAsync(string path, string bearerToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
            _env.Dispose();
        }
    }

    private sealed class FakeOidcServer : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly RSA _rsa;
        private readonly string _kid;

        private string _authorityRoot = string.Empty;
        private string _issuer = string.Empty;
        private string _metadataAddress = string.Empty;
        private string _jwksUri = string.Empty;

        private long _metadataRequests;
        private long _jwksRequests;

        private FakeOidcServer(WebApplication app, RSA rsa, string kid)
        {
            _app = app;
            _rsa = rsa;
            _kid = kid;
        }

        public string Issuer => _issuer;
        public string AuthorityRoot => _authorityRoot;
        public string MetadataAddress => _metadataAddress;
        public string JwksUri => _jwksUri;

        public long MetadataRequests => Interlocked.Read(ref _metadataRequests);
        public long JwksRequests => Interlocked.Read(ref _jwksRequests);

        public static Task<FakeOidcServer> StartAsync()
            => StartAsync(issuerPath: string.Empty, jwksPath: "/discovery/v2.0/keys");

        public static async Task<FakeOidcServer> StartAsync(string issuerPath, string jwksPath)
        {
            var rsa = RSA.Create(2048);
            var kid = Guid.NewGuid().ToString("N");

            var port = GetFreeTcpPort();

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

            var app = builder.Build();

            var server = new FakeOidcServer(app, rsa, kid);

            var normalizedIssuerPath = NormalizePath(issuerPath);
            var normalizedJwksPath = NormalizePath(jwksPath);
            var metadataPath = NormalizePath($"{normalizedIssuerPath}/.well-known/openid-configuration");

            server._authorityRoot = $"http://127.0.0.1:{port}";
            server._issuer = $"{server._authorityRoot}{normalizedIssuerPath}";
            server._jwksUri = $"{server._authorityRoot}{normalizedJwksPath}";
            server._metadataAddress = $"{server._authorityRoot}{metadataPath}";

            app.MapGet(metadataPath, () =>
            {
                Interlocked.Increment(ref server._metadataRequests);
                return Results.Json(new
                {
                    issuer = server._issuer,
                    jwks_uri = server._jwksUri,
                    id_token_signing_alg_values_supported = new[] { "RS256" }
                });
            });

            app.MapGet(normalizedJwksPath, () =>
            {
                Interlocked.Increment(ref server._jwksRequests);
                return Results.Json(new
                {
                    keys = new[] { CreateJwk(rsa, kid) }
                });
            });

            await app.StartAsync();

            return server;
        }

        public string CreateAccessToken(
            string audience,
            string? scopeClaimType,
            string? scopeValue,
            string[]? roles = null,
            string sub = "user-123",
            string email = "user@example.com",
            string tenantId = "tenant-123",
            Claim[]? extraClaims = null)
        {
            var now = DateTime.UtcNow;

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, sub),
                new("email", email),
                new("tid", tenantId),
                new("name", "Test User")
            };

            if (!string.IsNullOrWhiteSpace(scopeClaimType) && !string.IsNullOrWhiteSpace(scopeValue))
            {
                claims.Add(new Claim(scopeClaimType!, scopeValue!));
            }

            if (roles is not null)
            {
                claims.AddRange(roles.Select(r => new Claim("roles", r)));
            }

            if (extraClaims is not null)
            {
                claims.AddRange(extraClaims);
            }

            var key = new RsaSecurityKey(_rsa) { KeyId = _kid };
            var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: audience,
                claims: claims,
                notBefore: now.AddMinutes(-1),
                expires: now.AddMinutes(5),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static object CreateJwk(RSA rsa, string kid)
        {
            var parameters = rsa.ExportParameters(false);
            return new
            {
                kty = "RSA",
                use = "sig",
                kid,
                alg = "RS256",
                n = Base64UrlEncoder.Encode(parameters.Modulus),
                e = Base64UrlEncoder.Encode(parameters.Exponent)
            };
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _rsa.Dispose();
        }

        private static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var normalized = path.Trim();
            if (normalized.Length == 1 && normalized[0] == '/')
            {
                return string.Empty;
            }

            return normalized.StartsWith('/') ? normalized : $"/{normalized}";
        }
    }

    private sealed class EnvVarScope : IDisposable
    {
        private readonly IReadOnlyDictionary<string, string?> _previousValues;

        public EnvVarScope(IReadOnlyDictionary<string, string?> values)
        {
            var previous = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var (key, value) in values)
            {
                previous[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }

            _previousValues = previous;
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previousValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
