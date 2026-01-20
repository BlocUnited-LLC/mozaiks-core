
using AuthServer.Api.Controllers;
using AuthServer.Api.Filters;
using AuthServer.Api.Infrastructure;
using AuthServer.Api.Middlewares;
using AuthServer.Api.Repository;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Services;
using AuthServer.Api.Shared;
using AuthServer.Api.Workers;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
using Mozaiks.Auditing;
using Mozaiks.Auth;
using MongoDB.Driver;
using Serilog;
using Stripe;
using System.Net.Http.Headers;
using System.IO;
using System.Text;



var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault configuration (production)
var keyVaultUrl = builder.Configuration["KeyVault:Url"];
if (!string.IsNullOrWhiteSpace(keyVaultUrl))
{
    try
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultUrl),
            new DefaultAzureCredential());
        Console.WriteLine($"[Startup] Azure Key Vault configured: {keyVaultUrl}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Warning: Failed to connect to Key Vault: {ex.Message}");
        // Continue without Key Vault - use environment variables instead
    }
}

builder.AddServiceDefaults();

builder.AddForwardedHeadersForReverseProxy();

MongoConventions.Register();

// Add services to the container.

builder.Services.AddMongoAdminAuditing("AuthServer.Api");
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<AdminAuditActionFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

var dataProtectionKeysPath = (builder.Configuration.GetValue<string>("DataProtection:KeysPath") ?? string.Empty).Trim();
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("Mozaiks.AuthServer.Api");

if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    Directory.CreateDirectory(dataProtectionKeysPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

var paymentsSecretKey = builder.Configuration.GetValue<string>("Payments:SecretKey");
if (!string.IsNullOrWhiteSpace(paymentsSecretKey))
{
    StripeConfiguration.ApiKey = paymentsSecretKey;
}

builder.Services.AddSingleton<IMongoClient>(x =>
{
    var connectionString = builder.Configuration.GetValue<string>("MongoDB:ConnectionString");
    return new MongoClient(connectionString);
});

builder.Services.AddScoped<IMongoDatabase>(s =>
{
    var client = s.GetService<IMongoClient>();
    var databaseName = builder.Configuration.GetValue<string>("MongoDB:DatabaseName");
    return client.GetDatabase(databaseName);
});

builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection("ApiKeys"));
builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection("GitHub"));
builder.Services.Configure<GitHubSecretsOptions>(builder.Configuration.GetSection("GitHubSecrets"));
builder.Services.Configure<DeploymentOptions>(builder.Configuration.GetSection("Deployment"));
builder.Services.Configure<DatabaseProvisioningOptions>(builder.Configuration.GetSection("DatabaseProvisioning"));
builder.Services.Configure<MonetizationPolicyOptions>(builder.Configuration.GetSection("MonetizationPolicy"));
builder.Services.Configure<AuthServer.Api.Models.ProvisioningAgentOptions>(builder.Configuration.GetSection("ProvisioningAgent"));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IExternalLoginRepository, ExternalLoginRepository>();
builder.Services.AddScoped<IMozaiksAppRepository, MozaiksAppRepository>();
builder.Services.AddScoped<MozaiksAppService>();
builder.Services.AddScoped<IAppBuildStatusRepository, AppBuildStatusRepository>();
builder.Services.AddScoped<IAppBuildEventRepository, AppBuildEventRepository>();
builder.Services.AddScoped<IAppLifecycleService, AppLifecycleService>();
builder.Services.AddScoped<ICreatorDashboardService, CreatorDashboardService>();
builder.Services.AddScoped<IUserSettingsService, UserSettingsService>();
builder.Services.AddScoped<IInviteRepository, InviteRepository>();
builder.Services.AddScoped<InviteService>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<RoleService>();
builder.Services.AddScoped<IPermissionRepository, PermissionRepository>();
builder.Services.AddScoped<PermissionService>();
// NOTE: Wallet functionality has been consolidated into Payment.API service.
// All wallet operations (create, balance, transactions) should go through Payment.API.
// BLOCKCHAIN INTEGRATION POINT: When adding crypto wallets, register the client here
// that communicates with Payment.API's unified wallet service.

builder.Services.AddScoped<ITeamMembersRepository, TeamMembersRepository>();
builder.Services.AddScoped<IAppAdminSurfaceRepository, AppAdminSurfaceRepository>();
builder.Services.AddScoped<IAppModuleProxyAuditRepository, AppModuleProxyAuditRepository>();
builder.Services.AddScoped<IAppMonetizationSpecRepository, AppMonetizationSpecRepository>();
builder.Services.AddScoped<IAppMonetizationAuditRepository, AppMonetizationAuditRepository>();
builder.Services.AddScoped<MongoIndexInitializer>();
builder.Services.AddScoped<IDeploymentJobRepository, DeploymentJobRepository>();
builder.Services.AddScoped<IGitHubIntegrationService, GitHubIntegrationService>();
builder.Services.AddScoped<IDeploymentService, DeploymentService>();
builder.Services.AddScoped<IGitHubRepoExportService, GitHubRepoExportService>();
builder.Services.AddScoped<IAppModuleProxyService, AppModuleProxyService>();
builder.Services.AddSingleton<IDatabaseProvisioningService, DatabaseProvisioningService>();
builder.Services.AddScoped<IDeploymentTemplateService, DeploymentTemplateService>();
builder.Services.AddScoped<IMozaiksCoreService, MozaiksCoreService>();
builder.Services.AddScoped<IScaffoldService, ScaffoldService>();
builder.Services.AddHostedService<DeploymentJobWorker>();

// Lifecycle Phase Resolution
builder.Services.AddHttpClient<IHostingApiClient, HostingApiClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<IAppLifecyclePhaseResolver, AppLifecyclePhaseResolver>();

builder.Services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
builder.Services.AddScoped<SubscriptionPlanService>();
builder.Services.AddScoped<AppEntitlementsService>();
builder.Services.AddScoped<AppMonetizationSpecValidator>();
builder.Services.AddScoped<AppMonetizationPolicyEvaluator>();
builder.Services.AddScoped<AppMonetizationAuditService>();
builder.Services.AddScoped<AppMonetizationCommitService>();

builder.Services.AddSingleton<StructuredLogEmitter>();

//builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
//{
//    var configuration = builder.Configuration.GetSection("Redis:ConnectionString").Value;
//    return ConnectionMultiplexer.Connect(configuration);
//});

//builder.Services.AddScoped<RedisCacheService>();

builder.Services.AddScoped<CustomExceptionHandlerMiddleware>();

builder.Services.AddHttpClient("GitHub", (sp, client) =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MozaiksBackend/1.0");

    var cfg = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
    var token = (cfg.AccessToken ?? string.Empty).Trim();
    if (!string.IsNullOrWhiteSpace(token) && !string.Equals(token, "replace_me", StringComparison.OrdinalIgnoreCase))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
});

builder.Services.AddHttpClient("AppAdminProxy", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<IServiceToServiceTokenProvider, ServiceToServiceTokenProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<MozaiksPayClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<IMonetizationStripeProvisioner, MonetizationStripeProvisioner>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<ProvisioningAgentClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("BuildArtifacts", client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddHttpClient<NotificationApiClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});


builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Moz Server", Version = "v1" });

    // Include the Authorization header in Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.OperationFilter<FileUploadOperationFilter>();

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] { }
        }
    });
});



var Cors = builder.Configuration["AllowedOrigins"].Split(new string[1] { "," }, StringSplitOptions.RemoveEmptyEntries);

// CORS + JWT hardening (wildcard only in Development; fail-fast in non-dev)
builder.AddStrictCorsPolicy("CorsPolicy", "AllowedOrigins");
builder.AddMozaiksAuth();
builder.Services.AddAuthorization();

builder.Services.AddHttpClient<InviteController>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("MicroServiceEndpoints:NotificationService"));
});
builder.Services.AddHttpClient<GovernanceApiClient>(client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("MicroServiceEndpoints:GovernanceService");
    if (!string.IsNullOrWhiteSpace(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
});
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
    loggingBuilder.SetMinimumLevel(LogLevel.Debug);
});


//var logger = new LoggerConfiguration()
//    .ReadFrom.Configuration(builder.Configuration)

//    .Enrich.FromLogContext()
//    .WriteTo.Console()

//    .CreateLogger();
//builder.Logging.ClearProviders();
//builder.Logging.AddSerilog(logger);


try
{
    var logger = new LoggerConfiguration()
                        .ReadFrom.Configuration(builder.Configuration)
                        .Enrich.FromLogContext()
                        .CreateLogger();
    builder.Host.UseSerilog(logger);

    var app = builder.Build();

    var runningInContainer = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(dataProtectionKeysPath) && (runningInContainer || !app.Environment.IsDevelopment()))
    {
        app.Logger.LogWarning(
            "Data Protection key persistence is not configured. Encrypted admin-surface keys may become undecryptable after restarts/scale-out. " +
            "Configure a shared key ring via `DataProtection:KeysPath` (mounted volume/shared storage) to ensure admin keys remain decryptable.");
    }


app.UseMiddleware<CustomExceptionHandlerMiddleware>();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors("CorsPolicy");


// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSerilogRequestLogging();


app.UseAuthentication();
app.UseAuthorization();

// Ensure MongoDB indexes exist (Teams/Invites etc.) before serving traffic.
using (var scope = app.Services.CreateScope())
{
    var indexInitializer = scope.ServiceProvider.GetRequiredService<MongoIndexInitializer>();
    await indexInitializer.InitializeAsync();
}

app.MapControllers();

app.UseSerilogRequestLogging();

   
 app.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}

//logger.MapDefaultEndpoints();
 
