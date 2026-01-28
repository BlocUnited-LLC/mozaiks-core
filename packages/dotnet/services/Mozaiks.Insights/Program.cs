using Insights.API.Health;
using Insights.API.Infrastructure;
using Insights.API.Repository;
using Insights.API.Shared;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using Mozaiks.ApiKeys;
using Mozaiks.Auditing;
using Mozaiks.Auth;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

MongoConventions.Register();

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddMongoAdminAuditing("Insights.API");
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<AdminAuditActionFilter>();
})
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();

var mongoConnectionString = builder.Configuration.GetValue<string>("MongoDB:ConnectionString");
var mongoDatabaseName = builder.Configuration.GetValue<string>("MongoDB:DatabaseName");
if (string.IsNullOrWhiteSpace(mongoConnectionString))
{
    throw new InvalidOperationException("MongoDB:ConnectionString must be configured for Insights.API");
}
if (string.IsNullOrWhiteSpace(mongoDatabaseName))
{
    throw new InvalidOperationException("MongoDB:DatabaseName must be configured for Insights.API");
}

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));

builder.Services.AddScoped<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoDatabaseName);
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddScoped<IHostedAppReadRepository, HostedAppReadRepository>();
builder.Services.AddScoped<IKpiPointRepository, KpiPointRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IMozaiksAppReadRepository, MozaiksAppReadRepository>();
builder.Services.AddScoped<ITeamMemberReadRepository, TeamMemberReadRepository>();
builder.Services.AddScoped<IApiKeyUsageRepository, ApiKeyUsageRepository>();
builder.Services.AddScoped<IApiKeyValidationService, ApiKeyValidationService>();
builder.Services.AddScoped<MongoIndexInitializer>();

builder.Services.Configure<InsightsIngestionOptions>(builder.Configuration.GetSection("InsightsIngestion"));

builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("mongodb");

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Mozaiks Insights", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var corsOrigins = (builder.Configuration["AllowedOrigins"] ?? string.Empty)
    .Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

if (!builder.Environment.IsDevelopment() && corsOrigins.Length == 0)
{
    throw new InvalidOperationException("AllowedOrigins must be configured for Insights.API in non-development environments");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        if (corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins);
        }
        else
        {
            policy.SetIsOriginAllowed(_ => true);
        }

        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.AddMozaiksAuth();
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var indexInitializer = scope.ServiceProvider.GetRequiredService<MongoIndexInitializer>();
    await indexInitializer.InitializeAsync();
}

app.UseExceptionHandler();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors("CorsPolicy");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();
